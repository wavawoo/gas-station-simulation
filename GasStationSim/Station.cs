using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GasStationSim
{
    // Модель станции: коллекция колонок, цены, наценки, запасы и генераторы.
    // Принцип: Station инкапсулирует бизнес-логику по распределению и генерации заявок.
    public class Station
    {
        public List<Pump> Pumps { get; set; } = new List<Pump>();                 // Список колонок
        public Dictionary<string, double> BasePrice { get; set; } = new Dictionary<string, double>(); // Базовые цены
        public Dictionary<string, double> MarkupPercent { get; set; } = new Dictionary<string, double>(); // Наценки
        public Dictionary<string, double> Inventory { get; set; } = new Dictionary<string, double>(); // Остатки топлива

        public Random random = new Random();                                      // Генератор случайных чисел
        private StreamWriter writer;                                              // Логгер
        private int carCounter = 0;                                               // Счётчик авто

        // Параметры распределений
        public bool UseUniform = true;                                            // Флаг: равномерное или нормальное
        public double UniformA = 0.0, UniformB = 20.0;                            // Границы равномерного распределения
        public double NormalMu = 10.0, NormalSigma = 3.0;                         // Параметры нормального распределения

        // Параметры обслуживания
        public double ServiceOverheadMin = 0.5;                                   // Базовое время на операцию, мин
        public double MinPerLiter = 0.03;                                         // Время на литр, мин

        // Эластичность спроса от цены: 1% наценки уменьшает поток на 0.03 (3%)
        public double PriceElasticityPerPercent = 0.03;

        public Station(StreamWriter w) { writer = w; }

        // Добавление колонки
        public void AddPump(Pump p) => Pumps.Add(p);

        // Возвращает коэффициент уменьшения потока из-за средней наценки
        public double GetAdjustedArrivalFactor()
        {
            if (MarkupPercent == null || MarkupPercent.Count == 0) return 1.0;
            double avgMarkup = MarkupPercent.Values.Average();
            double reduction = avgMarkup * PriceElasticityPerPercent;
            return Math.Max(0.01, 1.0 - reduction); // не даём деления на 0
        }

        // Генерация интервала между прибытием автомобилей в минутах — теперь учитывает симуляционное время
        public double SampleInterArrivalMinutes(DateTime simNow)
        {
            double baseVal;

            if (UseUniform)
                baseVal = UniformA + random.NextDouble() * (UniformB - UniformA);
            else
            {
                // Box-Muller для нормального распределения
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                double randStd = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
                baseVal = Math.Abs(NormalMu + NormalSigma * randStd);
            }

            double timeFactor = GetTimeOfDayFactor(simNow);
            double adjusted = baseVal / GetAdjustedArrivalFactor() / Math.Max(0.01, timeFactor);
            return Math.Max(0.1, adjusted); // минимум 0.1 минуты между прибытием
        }

        // Создание CarRequest с случайной стороной бака (50/50)
        public CarRequest GenerateCar(DateTime time, List<string> brands, double minVol, double maxVol)
        {
            carCounter++;
            var brand = brands[random.Next(brands.Count)];
            var vol = minVol + random.NextDouble() * (maxVol - minVol);

            // 50/50 сторона бака
            TankSide side = (random.NextDouble() < 0.5) ? TankSide.Left : TankSide.Right;

            var req = new CarRequest
            {
                Id = carCounter,
                ArrivalTime = time,
                Brand = brand,
                Volume = Math.Round(vol, 1),
                Side = side
            };

            writer.WriteLine($"Создан {req}");
            return req;
        }

        // Распределение машины по колонке с учётом марки, баланса очередей и стороны бака
        public Pump DispatchToPump(CarRequest req)
        {
            // Кандидаты по марке
            var candidates = Pumps.Where(p => p.Brand == req.Brand).ToList();
            if (!candidates.Any())
                candidates = Pumps.ToList();

            // Фильтр по доступности стороны колонок
            candidates = candidates.Where(p => IsCompatibleSide(p.Access, req.Side)).ToList();
            if (!candidates.Any())
            {
                // Если нет совместимой колонки — возвращаем наиболее близкую (позволяем уехать)
                // В логике модели машина не может заправиться — будет обработано при попытке встать в очередь.
                writer.WriteLine($"Нет совместимых колонок по стороне для авто {req.Id} (side={req.Side}).");
                // Возвращаем колонку с минимальной очередью среди всех
                return Pumps.OrderBy(p => p.Queue.Count).First();
            }

            // Балансировка: разница между max и min очередью у колонок одного типа не должна превышать 2
            int maxQ = candidates.Max(p => p.Queue.Count);
            int minQ = candidates.Min(p => p.Queue.Count);

            if (maxQ - minQ > 2)
            {
                candidates = candidates.Where(p => p.Queue.Count <= minQ + 1).ToList();
            }

            // Выбираем колонку с минимальной очередью, затем по номеру (стабильность выбора)
            var chosen = candidates.OrderBy(p => p.Queue.Count).ThenBy(p => p.Number).First();
            return chosen;
        }

        // Проверка совместимости стороны колонки и машины
        private bool IsCompatibleSide(PumpAccess access, TankSide side)
        {
            if (access == PumpAccess.Both) return true;
            if (access == PumpAccess.LeftOnly && side == TankSide.Left) return true;
            if (access == PumpAccess.RightOnly && side == TankSide.Right) return true;
            return false;
        }

        // Вычисление времени обслуживания (минуты) по объёму: учитывается overhead + время на литр
        public double ComputeServiceTimeMinutes(double volume)
        {
            double baseTime = ServiceOverheadMin;
            double timeForLiters = MinPerLiter * volume;
            // Ограничиваем диапазон примерно между 1 и 3 минут для 10-50 л
            return Math.Max(0.5, Math.Min(10.0, baseTime + timeForLiters));
        }

        // Фактор интенсивности в зависимости от времени суток и дня недели
        public double GetTimeOfDayFactor(DateTime time)
        {
            int hour = time.Hour;
            int dayOfWeek = (int)time.DayOfWeek; // 0=Sunday

            double factor = 1.0;
            if (hour >= 7 && hour <= 9) factor *= 1.6; // утренний пик
            else if (hour >= 17 && hour <= 19) factor *= 1.4; // вечерний пик
            else if (hour >= 22 || hour <= 5) factor *= 0.4; // ночь
            else factor *= 1.0;

            if (dayOfWeek == 0 || dayOfWeek == 6) factor *= 1.3; // выходные

            return factor;
        }

        // Подсчёт прибыли станции (по наценкам)
        public double CalculateTotalProfit()
        {
            double totalProfit = 0;
            foreach (var pump in Pumps)
            {
                if (BasePrice.ContainsKey(pump.Brand) && MarkupPercent.ContainsKey(pump.Brand))
                {
                    double price = BasePrice[pump.Brand];
                    double markup = MarkupPercent[pump.Brand];
                    double revenue = pump.ServedLiters * price * (markup / 100.0);
                    totalProfit += revenue;
                }
            }
            return totalProfit;
        }
    }
}
