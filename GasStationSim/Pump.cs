using System;
using System;
using System.Collections.Generic;
using System.IO;

namespace GasStationSim
{
    // Топливная колонка (FuelPump).
    // SOLID: Pump отвечает только за состояние и очередь колонки (SRP).
    public class Pump
    {
        public int Number { get; set; }                 // Номер колонки
        public string Brand { get; set; }               // Марка топлива
        public Queue<CarRequest> Queue { get; set; } = new Queue<CarRequest>(); // Очередь
        public int MaxQueueLength { get; set; }         // Максимальная длина очереди
        public bool IsBusy { get; set; } = false;       // Занятость колонки

        public double ServedLiters { get; set; } = 0;   // Суммарно продано литров
        public int ServedCars { get; set; } = 0;        // Количество обслуженных авто
        public int LostCars { get; set; } = 0;          // Количество уехавших авто

        public PumpAccess Access { get; set; } = PumpAccess.Both; // Доступность колонки

        private StreamWriter writer;                    // Логгер (инъекция в конструкторе)

        public Pump(int number, string brand, int maxQueue, StreamWriter w)
        {
            Number = number;
            Brand = brand;
            MaxQueueLength = maxQueue;
            writer = w;
        }

        // Попытка встать в очередь; возвращает false и учитывает потерю, если очередь переполнена.
        public bool TryEnqueue(CarRequest req)
        {
            if (Queue.Count >= MaxQueueLength)
            {
                LostCars++;
                writer.WriteLine($"[{req.ArrivalTime:yyyy-MM-dd HH:mm}] Авто {req.Id} уехало — очередь переполнена на колонке {Number}.");
                return false;
            }

            Queue.Enqueue(req);
            writer.WriteLine($"[{req.ArrivalTime:yyyy-MM-dd HH:mm}] Авто {req.Id} в очереди к колонке {Number}. Очередь: {Queue.Count}/{MaxQueueLength}");
            return true;
        }

        // Извлечь следующую заявку
        public CarRequest Dequeue()
        {
            if (Queue.Count == 0) return null;
            return Queue.Dequeue();
        }
    }
}
