using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMO_Kalkylator
{
    public class SmoTypeInfo
    {
        public SmoType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool ShowChannels { get; set; }   // ввод числа каналов n
        public bool ShowQueueLimit { get; set; } // ввод длины очереди m
        public bool ShowPatience { get; set; }   // ввод времени ожидания / интенсивности ухода

        public override string ToString() => Title;
    }

    /// <summary>Список типов СМО, из которого строится меню.</summary>
    public static class SmoCatalog
    {
        public static readonly SmoTypeInfo[] Types =
        {
            new SmoTypeInfo
            {
                Type = SmoType.SingleRefusal,
                Title = "Одноканальная СМО с отказами",
                Description = "Один канал, мест в очереди нет. Заявка, заставшая канал занятым, получает отказ."
            },
            new SmoTypeInfo
            {
                Type = SmoType.MultiRefusal, ShowChannels = true,
                Title = "Многоканальная СМО с отказами (задача Эрланга)",
                Description = "n каналов, очереди нет. Заявка, заставшая все каналы занятыми, покидает систему."
            },
            new SmoTypeInfo
            {
                Type = SmoType.SingleUnlimited,
                Title = "Одноканальная СМО с неограниченной очередью",
                Description = "Один канал, очередь не ограничена. Стационарный режим существует при ρ = λ/μ < 1."
            },
            new SmoTypeInfo
            {
                Type = SmoType.MultiUnlimited, ShowChannels = true,
                Title = "Многоканальная СМО с неограниченной очередью",
                Description = "n каналов, очередь не ограничена. Стационарный режим существует при ρ/n < 1."
            },
            new SmoTypeInfo
            {
                Type = SmoType.SingleLimited, ShowQueueLimit = true,
                Title = "Одноканальная СМО с ограниченной очередью",
                Description = "Один канал, в очереди не более m заявок. При заполненной очереди заявка получает отказ."
            },
            new SmoTypeInfo
            {
                Type = SmoType.MultiLimited, ShowChannels = true, ShowQueueLimit = true,
                Title = "Многоканальная СМО с ограниченной очередью",
                Description = "n каналов, в очереди не более m заявок. При заполненной очереди заявка получает отказ."
            },
            new SmoTypeInfo
            {
                Type = SmoType.SingleFixedService,
                Title = "Одноканальная СМО с фиксированным временем обслуживания",
                Description = "Один канал, очередь не ограничена, время обслуживания постоянно и равно 1/μ (система M/D/1). Нужно ρ < 1."
            },
            new SmoTypeInfo
            {
                Type = SmoType.MultiImpatient, ShowChannels = true, ShowPatience = true,
                Title = "СМО с ограниченным временем ожидания («нетерпеливые» заявки)",
                Description = "n каналов, очередь не ограничена по длине, но заявка уходит, если ждёт дольше случайного времени " +
                              "(показательное распределение с интенсивностью ухода ν = 1/t_ож)."
            }
        };
    }
}
