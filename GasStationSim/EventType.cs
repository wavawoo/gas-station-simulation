namespace GasStationSim
{
    // Типы событий в симуляции
    public enum EventType
    {
        Arrival,        // Прибытие автомобиля
        ServiceStart,   // Начало обслуживания
        ServiceEnd,     // Завершение обслуживания
        DayReport       // Формирование дневного отчёта
    }
}
