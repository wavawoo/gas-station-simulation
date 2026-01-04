using System;
using System.Collections.Generic;
using System.IO;

namespace GasStationSim
{
    // Точка входа: отвечает только за ввод параметров и запуск симуляции (SRP: Single Responsibility Principle)
    public class Program
    {
        // Диапазоны допустимых значений (константы доменной логики)
        private const int MinPumps = 1;
        private const int MaxPumps = 20;
        private const int MinQueueLen = 1;
        private const int MaxQueueLen = 20;

        public static void Main()
        {
            Console.WriteLine("GasStationSim — параметры моделирования");

            // Кол-во колонок (с проверкой реалистичного диапазона)
            int K = ReadIntWithPrompt("Введите количество колонок: ",
                                      MinPumps, MaxPumps,
                                      tooLargeMsg: $"Слишком много колонок — станция физически не может разместить столько оборудования. Введите значение от {MinPumps} до {MaxPumps}.");

            // Максимальная длина очереди
            int N = ReadIntWithPrompt("Введите максимальную длину очереди у каждой колонки: ",
                                      MinQueueLen, MaxQueueLen,
                                      tooLargeMsg: $"Нереалистично длинная очередь — на территории станции нет столько места. Разрешён диапазон {MinQueueLen}..{MaxQueueLen}.");

            // Срок моделирования
            int days = ReadIntWithPrompt("Введите длительность моделирования в днях: ", 1, 30);

            // Наценки топлива (экономический параметр симуляции)
            Console.WriteLine("Введите торговые наценки (%) для марок топлива.");
            double markup92 = ReadDoubleWithDefault("Наценка A92 [%] (default 7): ", 7.0);
            double markup95 = ReadDoubleWithDefault("Наценка A95 [%] (default 8): ", 8.0);
            double markupDiesel = ReadDoubleWithDefault("Наценка Diesel [%] (default 6): ", 6.0);

            // Начальные запасы топлива
            Console.WriteLine("Начальные запасы топлива (литры). Enter = значение по умолчанию.");
            double inv92 = ReadDoubleWithDefault("Запас A92 [default 5000]: ", 5000);
            double inv95 = ReadDoubleWithDefault("Запас A95 [default 4000]: ", 4000);
            double invDiesel = ReadDoubleWithDefault("Запас Diesel [default 3000]: ", 3000);

            // Поддерживаемые марки топлива
            var brands = new List<string> { "A92", "A95", "Diesel" };

            using (StreamWriter writer = new StreamWriter("simulation_output.txt"))
            {
                // Создание станции
                var station = new Station(writer)
                {
                    UseUniform = true,
                    UniformA = 0.5,      // Исправлено: минимум 0.5 мин
                    UniformB = 4.0       // Исправлено: максимум 4 мин
                };

                // Базовые цены
                station.BasePrice["A92"] = 48.0;
                station.BasePrice["A95"] = 52.0;
                station.BasePrice["Diesel"] = 49.5;

                // Наценки
                station.MarkupPercent["A92"] = markup92;
                station.MarkupPercent["A95"] = markup95;
                station.MarkupPercent["Diesel"] = markupDiesel;

                // Складские запасы
                station.Inventory["A92"] = inv92;
                station.Inventory["A95"] = inv95;
                station.Inventory["Diesel"] = invDiesel;

                // Создание колонок (все двусторонние — PumpAccess.Both)
                for (int i = 0; i < K; i++)
                {
                    var pump = new Pump(i + 1, brands[i % brands.Count], N, writer)
                    {
                        Access = PumpAccess.Both
                    };
                    station.AddPump(pump);
                }

                // Параметры генерации объёма заправки
                double minVol = 10.0;   // мин. объём, литры
                double maxVol = 50.0;   // макс. объём, литры

                // Период симуляции: с полуночи текущего дня
                DateTime start = DateTime.Today;
                DateTime end = start.AddDays(days);

                // Запуск движка — IoC: SimulationEngine работает с абстракциями Station и Pump
                var engine = new SimulationEngine(station, start, end, writer, brands, minVol, maxVol);
                engine.Run();

                Console.WriteLine("Симуляция завершена. Результаты записаны в simulation_output.txt");
            }
        }

        // Универсальный метод чтения int в диапазоне [min..max] с повторами (вариант А)
        private static int ReadIntWithPrompt(string prompt, int min, int max, string tooLargeMsg = null)
        {
            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine();

                if (!int.TryParse(input, out int value))
                {
                    Console.WriteLine("Ошибка: требуется целое число.");
                    continue;
                }

                if (value < min)
                {
                    Console.WriteLine($"Ошибка: число должно быть >= {min}.");
                    continue;
                }

                if (value > max)
                {
                    Console.WriteLine(tooLargeMsg ?? $"Ошибка: число должно быть <= {max}.");
                    continue;
                }

                return value;
            }
        }

        // Чтение double, допускает пустую строку → default
        private static double ReadDoubleWithDefault(string prompt, double defaultVal)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultVal;

            if (!double.TryParse(input.Replace(',', '.'), out double value))
            {
                Console.WriteLine("Неверный формат числа. Используется значение по умолчанию.");
                return defaultVal;
            }

            if (value < 0)
            {
                Console.WriteLine("Число не может быть отрицательным. Используется значение по умолчанию.");
                return defaultVal;
            }

            return value;
        }
    }
}
