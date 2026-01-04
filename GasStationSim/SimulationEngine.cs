using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GasStationSim
{
    // SimulationEngine — движок дискретно-событийной симуляции (DES).
    // Принцип SRP: отвечает только за обработку событий.
    // Принцип IoC/DIP: работает через абстракции Station и Pump, не создаёт их самостоятельно.
    public class SimulationEngine
    {
        private Station station;                        // Станция (модель)
        private PriorityQueue<SimEvent> queue = new();  // Приоритетная очередь событий (DES-event queue)
        private DateTime now;                           // Текущее симуляционное время
        private DateTime end;                           // Время завершения симуляции
        private StreamWriter writer;                    // Логгер
        private List<string> brands;                    // Поддерживаемые марки топлива
        private double minVol, maxVol;                  // Диапазон объёмов заправки

        public SimulationEngine(Station s, DateTime start, DateTime endTime,
                                StreamWriter w, List<string> brandsList,
                                double minV, double maxV)
        {
            station = s;      // DI — передаём зависимость извне
            now = start;
            end = endTime;
            writer = w;
            brands = brandsList;
            minVol = minV;
            maxVol = maxV;
        }

        // Основной цикл
        public void Run()
        {
            writer.WriteLine($"Симуляция: {now:yyyy-MM-dd} .. {end:yyyy-MM-dd}");

            // Первое прибытие автомобиля
            double firstDelta = station.SampleInterArrivalMinutes(now);
            ScheduleEvent(new SimEvent { Time = now.AddMinutes(firstDelta), Type = EventType.Arrival });

            // Планирование ежедневных отчётов
            DateTime dayMark = now.Date.AddDays(1);
            while (dayMark <= end)
            {
                ScheduleEvent(new SimEvent { Time = dayMark, Type = EventType.DayReport });
                dayMark = dayMark.AddDays(1);
            }

            // Пока есть события — обрабатываем по времени
            while (queue.Count > 0)
            {
                var ev = queue.Dequeue();
                now = ev.Time;

                if (now > end) break;

                ProcessEvent(ev);
            }

            WriteFinalStats();
            writer.Flush();
        }

        // Добавление события в очередь
        private void ScheduleEvent(SimEvent ev) => queue.Enqueue(ev);

        // Диспетчеризация событий
        private void ProcessEvent(SimEvent ev)
        {
            switch (ev.Type)
            {
                case EventType.Arrival: HandleArrival(ev); break;
                case EventType.ServiceStart: HandleServiceStart(ev); break;
                case EventType.ServiceEnd: HandleServiceEnd(ev); break;
                case EventType.DayReport: HandleDayReport(ev); break;
            }
        }

        // Обработка прибытия автомобиля 
        private void HandleArrival(SimEvent ev)
        {
            var car = station.GenerateCar(ev.Time, brands, minVol, maxVol);
            var pump = station.DispatchToPump(car);

            bool added = pump.TryEnqueue(car);
            if (!added)
                car.IsLost = true;

            // Если колонка свободна — начинаем обслуживание со сдвигом +1 сек 
            if (!pump.IsBusy && pump.Queue.Count > 0)
                StartServiceAtPump(pump, ev.Time.AddSeconds(1));

            // Планируем следующее прибытие
            double delta = station.SampleInterArrivalMinutes(ev.Time);
            ScheduleEvent(new SimEvent
            {
                Time = ev.Time.AddMinutes(delta),
                Type = EventType.Arrival
            });
        }

        // Начало обслуживания автомобиля 
        private void HandleServiceStart(SimEvent ev)
        {
            var pump = ev.Payload as Pump;
            if (pump == null || pump.IsBusy) return;

            var req = pump.Dequeue();
            if (req == null)
            {
                pump.IsBusy = false;
                return;
            }

            // Проверка запасов топлива
            if (station.Inventory.ContainsKey(req.Brand) &&
                station.Inventory[req.Brand] < req.Volume)
            {
                // Потеря клиента из-за отсутствия топлива
                req.IsLost = true;
                pump.LostCars++;

                writer.WriteLine($"[{ev.Time:yyyy-MM-dd HH:mm}] Авто {req.Id} не обслужено — недостаточно топлива ({req.Brand}).");

                // Попытка запустить следующего в очереди
                if (pump.Queue.Count > 0)
                    StartServiceAtPump(pump, ev.Time.AddSeconds(1));
                return;
            }

            pump.IsBusy = true;
            station.Inventory[req.Brand] -= req.Volume;

            double serviceMin = station.ComputeServiceTimeMinutes(req.Volume);

            writer.WriteLine($"[{ev.Time:yyyy-MM-dd HH:mm}] Колонка {pump.Number} начала заправку авто {req.Id} ({req.Volume:F1} л).");

            // Планируем завершение обслуживания
            ScheduleEvent(new SimEvent
            {
                Time = ev.Time.AddMinutes(serviceMin),
                Type = EventType.ServiceEnd,
                Payload = new ServicePayload { Pump = pump, Req = req }
            });
        }

        // Завершение обслуживания автомобиля 
        private void HandleServiceEnd(SimEvent ev)
        {
            var payload = ev.Payload as ServicePayload;

            Pump pump = payload.Pump;
            CarRequest req = payload.Req;

            pump.IsBusy = false;
            req.IsServed = true;

            pump.ServedCars++;
            pump.ServedLiters += req.Volume;

            writer.WriteLine($"[{ev.Time:yyyy-MM-dd HH:mm}] Колонка {pump.Number} завершила обслуживание авто {req.Id}.");

            // Если очередь не пуста — обслуживаем следующего
            if (pump.Queue.Count > 0)
                StartServiceAtPump(pump, ev.Time.AddSeconds(1));
        }

        // Дневной отчёт
        private void HandleDayReport(SimEvent ev)
        {
            writer.WriteLine($"[{ev.Time:yyyy-MM-dd HH:mm}] Дневной отчёт за {ev.Time:yyyy-MM-dd}");
            foreach (var p in station.Pumps)
            {
                writer.WriteLine(
                    $"Колонка {p.Number}: обслужено={p.ServedCars}, потеряно={p.LostCars}, очередь={p.Queue.Count}, выдано={p.ServedLiters:F1} л");
            }
        }

        // Создание события начала обслуживания
        private void StartServiceAtPump(Pump pump, DateTime t)
        {
            ScheduleEvent(new SimEvent
            {
                Time = t,
                Type = EventType.ServiceStart,
                Payload = pump
            });
        }

        // Финальный отчёт по симуляции
        private void WriteFinalStats()
        {
            writer.WriteLine($"\nИтоговая сводка:");

            double totalLiters = 0;
            int totalCars = 0;
            int totalLost = 0;

            foreach (var p in station.Pumps)
            {
                writer.WriteLine(
                    $"Колонка {p.Number}: обслужено={p.ServedCars}, литры={p.ServedLiters:F1}, потеряно={p.LostCars}");

                totalLiters += p.ServedLiters;
                totalCars += p.ServedCars;
                totalLost += p.LostCars;
            }

            double profit = station.CalculateTotalProfit();

            writer.WriteLine($"Всего обслужено авто: {totalCars}");
            writer.WriteLine($"Всего продано топлива: {totalLiters:F1} л");
            writer.WriteLine($"Всего потеряно авто: {totalLost}");
            writer.WriteLine($"Прибыль станции: {profit:F2} руб");
        }

        // Класс-контейнер для передачи Pump+CarRequest в событии завершения обслуживания
        private class ServicePayload
        {
            public Pump Pump { get; set; }
            public CarRequest Req { get; set; }
        }
    }
}
