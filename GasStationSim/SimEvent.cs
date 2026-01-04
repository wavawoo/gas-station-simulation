using System;
using System;

namespace GasStationSim
{
    // Событие симуляции с временем и полезной нагрузкой.
    public class SimEvent : IComparable<SimEvent>
    {
        public DateTime Time { get; set; }    // Время события
        public EventType Type { get; set; }   // Тип события
        public object Payload { get; set; }   // Полезная нагрузка 

        public int CompareTo(SimEvent other)
        {
            return Time.CompareTo(other.Time);
        }
    }
}
