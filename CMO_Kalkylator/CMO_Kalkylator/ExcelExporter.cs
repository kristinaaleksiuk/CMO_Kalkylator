using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMO_Kalkylator
{
    /// <summary>Данные одного расчёта для выгрузки в Excel.</summary>
    public class ExportData
    {
        public SmoTypeInfo Info { get; set; }
        public double Lambda { get; set; }
        public double Mu { get; set; }
        public int Channels { get; set; }
        public int QueueLimit { get; set; }
        public double Nu { get; set; }
        public string Unit { get; set; }
        public CalcResult Result { get; set; }
    }

    public static class ExcelExporter
    {
        private static readonly XLColor HeaderColor = XLColor.FromHtml("#DCE6F1");

        public static void Export(string path, ExportData d)
        {
            var wb = new XLWorkbook();
            WriteResults(wb.AddWorksheet("Результаты"), d);
            if (d.Result.States.Count > 0)
                WriteStates(wb.AddWorksheet("Вероятности состояний"), d);
            wb.SaveAs(path);
        }

        // ----------------------------------------------------------------------------------
        private static void WriteResults(IXLWorksheet ws, ExportData d)
        {
            int row = 1;

            ws.Cell(row, 1).Value = "Анализ СМО";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            row++;

            ws.Cell(row, 1).Value = d.Info.Title;
            row++;
            ws.Cell(row, 1).Value = d.Info.Description;
            ws.Cell(row, 1).Style.Font.Italic = true;
            row += 2;

            // ---- исходные данные ----
            Header(ws, row, "Исходные данные", "Значение", "Обозначение");
            row++;
            string u = d.Unit;
            row = Input(ws, row, $"Интенсивность потока заявок, 1/{u}", d.Lambda, "λ");
            row = Input(ws, row, $"Интенсивность обслуживания, 1/{u}", d.Mu, "μ");
            if (d.Info.ShowChannels) row = Input(ws, row, "Число каналов", d.Channels, "n");
            if (d.Info.ShowQueueLimit) row = Input(ws, row, "Максимум мест в очереди", d.QueueLimit, "m");
            if (d.Info.ShowPatience) row = Input(ws, row, $"Интенсивность ухода из очереди, 1/{u}", d.Nu, "ν");
            row++;

            // ---- результаты ----
            Header(ws, row, "Характеристика эффективности", "Значение", "Формула");
            row++;
            int first = row;
            foreach (var r in d.Result.Rows)
            {
                ws.Cell(row, 1).Value = r.Name;
                ws.Cell(row, 2).Value = r.Number;                 // число, а не текст
                ws.Cell(row, 2).Style.NumberFormat.Format = "0.00000";
                ws.Cell(row, 3).Value = r.Formula;
                row++;
            }
            if (row > first)
                ws.Range(first - 1, 1, row - 1, 3).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            if (row > first)
                ws.Range(first - 1, 1, row - 1, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            if (!string.IsNullOrEmpty(d.Result.Warning))
            {
                row++;
                ws.Cell(row, 1).Value = "Примечание: " + d.Result.Warning;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.DarkRed;
            }

            ws.Column(1).Width = 58;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 62;
        }

        private static int Input(IXLWorksheet ws, int row, string name, double value, string symbol)
        {
            ws.Cell(row, 1).Value = name;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 3).Value = symbol;
            return row + 1;
        }

        private static void Header(IXLWorksheet ws, int row, string a, string b, string c)
        {
            ws.Cell(row, 1).Value = a;
            ws.Cell(row, 2).Value = b;
            ws.Cell(row, 3).Value = c;
            var range = ws.Range(row, 1, row, 3);
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = HeaderColor;
        }

        // ----------------------------------------------------------------------------------
        private static void WriteStates(IXLWorksheet ws, ExportData d)
        {
            Header(ws, 1, "Состояние", "Вероятность", "");
            int row = 2;
            foreach (var s in d.Result.States)
            {
                if (double.IsNaN(s.Number)) continue;             // пропускаем служебную строку «…»
                ws.Cell(row, 1).Value = s.State;
                ws.Cell(row, 2).Value = s.Number;
                ws.Cell(row, 2).Style.NumberFormat.Format = "0.00000";
                row++;
            }
            ws.Column(1).Width = 14;
            ws.Column(2).Width = 16;
        }
    }
}
