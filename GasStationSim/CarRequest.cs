using System;
namespace GasStationSim
{
    // Заявка автомобиля на заправку.
    // Single Responsibility: содержит только данные заявки.
    public class CarRequest
    {
        public int Id { get; set; }                  // Идентификатор
        public DateTime ArrivalTime { get; set; }    // Время прибытия (симуляционное)
        public string Brand { get; set; }            // Марка топлива
        public double Volume { get; set; }           // Объём заправки (л)
        public bool IsServed { get; set; }           // Обслужен ли
        public bool IsLost { get; set; }             // Уехал без обслуживания
        public TankSide Side { get; set; }           // Сторона бензобака (Left/Right)

        public override string ToString() =>
            $"Авто#{Id} [{Brand}] {Volume:F1} л, время {ArrivalTime:yyyy-MM-dd HH:mm}, side={Side}";
    }
}
