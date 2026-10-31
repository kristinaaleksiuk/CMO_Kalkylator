using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMO_Kalkylator
{
    /// <summary>Типы систем массового обслуживания (по презентации «Классификация СМО»)</summary>
    public enum SmoType
    {
        SingleRefusal,      // одноканальная с отказами
        MultiRefusal,       // многоканальная с отказами (задача Эрланга)
        SingleUnlimited,    // одноканальная с неограниченной очередью
        MultiUnlimited,     // многоканальная с неограниченной очередью
        SingleLimited,      // одноканальная с ограниченной очередью
        MultiLimited,       // многоканальная с ограниченной очередью
        SingleFixedService, // одноканальная с фиксированным временем обслуживания (M/D/1)
        MultiImpatient      // многоканальная с ограниченным временем ожидания («нетерпеливые» заявки)
    }

    public class ResultRow
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public double Number { get; set; }
        public string Formula { get; set; }
    }

    public class StateRow
    {
        public string State { get; set; }
        public string Probability { get; set; }
        public double Number { get; set; } = double.NaN;
    }

    public class CalcResult
    {
        public List<ResultRow> Rows { get; } = new List<ResultRow>();
        public List<StateRow> States { get; } = new List<StateRow>();
        public string Warning { get; set; }
    }

    public static class QueueingCalculator
    {
        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");
        private const int MaxStatesShown = 30;

        // ----------------------------------------------------------------------------------
        public static CalcResult Calculate(SmoType type, double lambda, double mu, int n, int m, double nu, string unit)
        {
            var res = new CalcResult();
            double rho = lambda / mu;
            string u = string.IsNullOrWhiteSpace(unit) ? "ед. времени" : unit.Trim();

            switch (type)
            {
                case SmoType.SingleRefusal:
                    Refusal(res, lambda, mu, 1, u);
                    break;
                case SmoType.MultiRefusal:
                    Refusal(res, lambda, mu, n, u);
                    break;
                case SmoType.SingleUnlimited:
                    Unlimited(res, lambda, mu, 1, u);
                    break;
                case SmoType.MultiUnlimited:
                    Unlimited(res, lambda, mu, n, u);
                    break;
                case SmoType.SingleLimited:
                    Limited(res, lambda, mu, 1, m, u);
                    break;
                case SmoType.MultiLimited:
                    Limited(res, lambda, mu, n, m, u);
                    break;
                case SmoType.SingleFixedService:
                    FixedService(res, lambda, mu, u);
                    break;
                case SmoType.MultiImpatient:
                    Impatient(res, lambda, mu, n, nu, u);
                    break;
            }
            return res;
        }

        // ---------------------------------- Вспомогательные ---------------------------------
        private static string F(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return "—";
            if (x != 0 && Math.Abs(x) < 1e-4) return x.ToString("0.###E+0", Ru);
            return x.ToString("0.#####", Ru);
        }

        private static void Add(CalcResult r, string name, double value, string formula)
            => r.Rows.Add(new ResultRow { Name = name, Value = F(value), Formula = formula });

        private static void AddStates(CalcResult r, IList<double> p)
        {
            for (int k = 0; k < p.Count && k < MaxStatesShown; k++)
                r.States.Add(new StateRow { State = "S" + k, Probability = F(p[k]) });
            if (p.Count > MaxStatesShown)
                r.States.Add(new StateRow { State = "…", Probability = "показаны первые " + MaxStatesShown });
        }

        /// <summary>Нормирует веса так, чтобы сумма равнялась 1.</summary>
        private static double[] Normalize(List<double> w)
        {
            double s = w.Sum();
            return w.Select(x => x / s).ToArray();
        }

        // ------------------------------- СМО с отказами -------------------------------------
        private static void Refusal(CalcResult r, double lambda, double mu, int n, string u)
        {
            double rho = lambda / mu;
            var w = new List<double> { 1.0 };
            for (int k = 1; k <= n; k++) w.Add(w[k - 1] * rho / k);   // ρ^k / k!
            double[] p = Normalize(w);

            double pRefuse = p[n];
            double q = 1 - pRefuse;
            double a = lambda * q;
            double busy = a / mu;

            Add(r, "Приведённая интенсивность потока ρ", rho, "ρ = λ / μ");
            Add(r, "Вероятность простоя системы p₀", p[0], "p₀ = 1 / Σ (ρᵏ/k!), k = 0..n");
            Add(r, "Вероятность отказа P_отк", pRefuse, "P_отк = pₙ = (ρⁿ/n!)·p₀");
            Add(r, "Относительная пропускная способность Q", q, "Q = 1 − P_отк");
            Add(r, "Абсолютная пропускная способность A, заявок/" + u, a, "A = λ·Q");
            Add(r, "Среднее число занятых каналов k̄", busy, "k̄ = A / μ = ρ·(1 − P_отк)");
            Add(r, "Среднее число свободных каналов", n - busy, "n − k̄");
            Add(r, "Коэффициент загрузки каналов", busy / n, "k̄ / n");
            AddStates(r, p);
        }


        // --------------------------- СМО с неограниченной очередью --------------------------
        private static void Unlimited(CalcResult r, double lambda, double mu, int n, string u)
        {
            double rho = lambda / mu;
            double chi = rho / n;
            Add(r, "Приведённая интенсивность потока ρ", rho, "ρ = λ / μ");
            if (n > 1) Add(r, "Загрузка на один канал ρ/n", chi, "χ = ρ / n");

            if (chi >= 1)
            {
                r.Warning = "Условие стационарности не выполняется (ρ/n ≥ 1): очередь неограниченно растёт, " +
                            "предельные вероятности не существуют. Увеличьте μ или число каналов.";
                return;
            }

            // p0 = [ Σ_{k=0..n} ρ^k/k!  +  ρ^{n+1} / (n!·(n−ρ)) ]^{-1}
            double term = 1, sum = 1;
            for (int k = 1; k <= n; k++) { term *= rho / k; sum += term; }
            double wn = term;                          // ρⁿ/n!
            sum += wn * rho / (n - rho);
            double p0 = 1 / sum;

            double pWait = wn / (1 - chi) * p0;        // вероятность, что все каналы заняты (заявка ждёт)
            double lq = wn * chi / ((1 - chi) * (1 - chi)) * p0;
            double lSys = lq + rho;
            double wq = lq / lambda;
            double wSys = lSys / lambda;

            Add(r, "Вероятность простоя системы p₀", p0, n == 1 ? "p₀ = 1 − ρ" : "p₀ = [Σ ρᵏ/k! + ρⁿ⁺¹/(n!(n−ρ))]⁻¹");
            Add(r, "Вероятность отказа P_отк", 0, "очередь не ограничена");
            Add(r, "Вероятность ожидания (все каналы заняты) P_ож", pWait, n == 1 ? "P_ож = ρ" : "P_ож = ρⁿ/(n!(1−ρ/n))·p₀");
            Add(r, "Средняя длина очереди L_оч", lq, n == 1 ? "L_оч = ρ²/(1 − ρ)" : "L_оч = ρⁿ⁺¹·p₀ / (n·n!·(1−ρ/n)²)");
            Add(r, "Среднее число заявок в системе L_сист", lSys, "L_сист = L_оч + ρ");
            Add(r, "Среднее время ожидания в очереди, " + u, wq, "t_оч = L_оч / λ  (формула Литтла)");
            Add(r, "Среднее время пребывания в системе, " + u, wSys, "t_сист = L_сист / λ  (формула Литтла)");
            Add(r, "Среднее число занятых каналов k̄", rho, "k̄ = ρ");
            Add(r, "Коэффициент загрузки каналов", chi, "ρ / n");
            Add(r, "Абсолютная пропускная способность A, заявок/" + u, lambda, "A = λ");

            // p_k для показа
            var p = new List<double>();
            double w = 1;
            for (int k = 0; k < MaxStatesShown; k++)
            {
                if (k > 0) w *= rho / Math.Min(k, n);
                p.Add(w * p0);
            }
            AddStates(r, p);
            r.States.Add(new StateRow { State = "…", Probability = "ряд бесконечный, показаны первые " + MaxStatesShown });
        }

        // --------------------------- СМО с ограниченной очередью ----------------------------
        private static void Limited(CalcResult r, double lambda, double mu, int n, int m, string u)
        {
            double rho = lambda / mu;
            var w = new List<double> { 1.0 };
            for (int k = 1; k <= n + m; k++) w.Add(w[k - 1] * rho / Math.Min(k, n));
            double[] p = Normalize(w);

            double pRefuse = p[n + m];
            double q = 1 - pRefuse;
            double a = lambda * q;
            double busy = a / mu;
            double lq = 0;
            for (int s = 1; s <= m; s++) lq += s * p[n + s];
            double pWait = 0;                                 // заявка встала в очередь
            for (int k = n; k < n + m; k++) pWait += p[k];
            double lSys = lq + busy;
            double wq = lq / a;
            double wSys = lSys / a;

            Add(r, "Приведённая интенсивность потока ρ", rho, "ρ = λ / μ");
            Add(r, "Вероятность простоя системы p₀", p[0], "p₀ = [Σ ρᵏ/k! + (ρⁿ/n!)·Σ (ρ/n)ˢ]⁻¹, s = 1..m");
            Add(r, "Вероятность отказа P_отк", pRefuse, "P_отк = p_{n+m} = ρⁿ⁺ᵐ/(nᵐ·n!)·p₀");
            Add(r, "Относительная пропускная способность Q", q, "Q = 1 − P_отк");
            Add(r, "Абсолютная пропускная способность A, заявок/" + u, a, "A = λ·Q");
            Add(r, "Вероятность ожидания (заявка встала в очередь)", pWait, "Σ pₖ, k = n..n+m−1");
            Add(r, "Средняя длина очереди L_оч", lq, "L_оч = Σ s·p_{n+s}, s = 1..m");
            Add(r, "Среднее число занятых каналов k̄", busy, "k̄ = A / μ");
            Add(r, "Среднее число заявок в системе L_сист", lSys, "L_сист = L_оч + k̄");
            Add(r, "Среднее время ожидания в очереди, " + u, wq, "t_оч = L_оч / A  (для принятых заявок)");
            Add(r, "Среднее время пребывания в системе, " + u, wSys, "t_сист = L_сист / A  (для принятых заявок)");
            Add(r, "Коэффициент загрузки каналов", busy / n, "k̄ / n");
            AddStates(r, p);
        }

        // ------------------------- СМО с фиксированным временем (M/D/1) ---------------------
        private static void FixedService(CalcResult r, double lambda, double mu, string u)
        {
            double rho = lambda / mu;
            Add(r, "Приведённая интенсивность потока ρ", rho, "ρ = λ / μ = λ·t_обс");
            if (rho >= 1)
            {
                r.Warning = "Условие стационарности не выполняется (ρ ≥ 1): очередь неограниченно растёт.";
                return;
            }
            double lq = rho * rho / (2 * (1 - rho));
            double lSys = lq + rho;
            double wq = lq / lambda;
            double wSys = wq + 1 / mu;

            Add(r, "Вероятность простоя системы p₀", 1 - rho, "p₀ = 1 − ρ");
            Add(r, "Вероятность отказа P_отк", 0, "очередь не ограничена");
            Add(r, "Вероятность ожидания P_ож", rho, "P_ож = ρ");
            Add(r, "Средняя длина очереди L_оч", lq, "L_оч = ρ² / (2(1 − ρ))  (формула Поллачека–Хинчина)");
            Add(r, "Среднее число заявок в системе L_сист", lSys, "L_сист = L_оч + ρ");
            Add(r, "Среднее время ожидания в очереди, " + u, wq, "t_оч = L_оч / λ");
            Add(r, "Среднее время пребывания в системе, " + u, wSys, "t_сист = t_оч + 1/μ");
            Add(r, "Коэффициент загрузки канала", rho, "ρ");
            Add(r, "Абсолютная пропускная способность A, заявок/" + u, lambda, "A = λ");
            r.Warning = "Для постоянного времени обслуживания распределение вероятностей состояний " +
                        "не показательное и здесь не выводится; средняя длина очереди в 2 раза меньше, чем для M/M/1.";
        }

        // ----------------------- СМО с ограниченным временем ожидания -----------------------
        private static void Impatient(CalcResult r, double lambda, double mu, int n, double nu, string u)
        {
            double rho = lambda / mu;
            // w_k = w_{k-1} · λ / (min(k,n)·μ + max(0,k−n)·ν); ряд сходится всегда
            var w = new List<double> { 1.0 };
            double sum = 1;
            for (int k = 1; k < 2_000_000; k++)
            {
                double denom = k <= n ? k * mu : n * mu + (k - n) * nu;
                double next = w[k - 1] * lambda / denom;
                w.Add(next);
                sum += next;
                if (k > n && next < 1e-18 * sum) break;
            }
            double[] p = Normalize(w);

            double lq = 0, pWait = 0;
            for (int k = n + 1; k < p.Length; k++) lq += (k - n) * p[k];
            for (int k = n; k < p.Length; k++) pWait += p[k];

            double leaveRate = nu * lq;                   // интенсивность ухода из очереди
            double pRefuse = leaveRate / lambda;
            double a = lambda - leaveRate;
            double busy = a / mu;
            double lSys = lq + busy;

            Add(r, "Приведённая интенсивность потока ρ", rho, "ρ = λ / μ");
            Add(r, "Интенсивность ухода «нетерпеливых» заявок ν, 1/" + u, nu, "ν = 1 / t_ож");
            Add(r, "Вероятность простоя системы p₀", p[0], "p₀ = [1 + Σ ∏ λ/(min(i,n)μ + max(0,i−n)ν)]⁻¹");
            Add(r, "Вероятность ожидания (все каналы заняты) P_ож", pWait, "Σ pₖ, k ≥ n");
            Add(r, "Вероятность отказа (уход из очереди) P_отк", pRefuse, "P_отк = ν·L_оч / λ");
            Add(r, "Относительная пропускная способность Q", 1 - pRefuse, "Q = 1 − P_отк");
            Add(r, "Абсолютная пропускная способность A, заявок/" + u, a, "A = λ·Q");
            Add(r, "Средняя длина очереди L_оч", lq, "L_оч = Σ s·p_{n+s}");
            Add(r, "Среднее число занятых каналов k̄", busy, "k̄ = A / μ");
            Add(r, "Среднее число заявок в системе L_сист", lSys, "L_сист = L_оч + k̄");
            Add(r, "Среднее время ожидания в очереди, " + u, lq / lambda, "t_оч = L_оч / λ  (на одну заявку потока)");
            Add(r, "Среднее время пребывания в системе, " + u, lSys / lambda, "t_сист = L_сист / λ  (на одну заявку потока)");
            Add(r, "Коэффициент загрузки каналов", busy / n, "k̄ / n");
            AddStates(r, p);
            r.States.Add(new StateRow { State = "…", Probability = "ряд бесконечный, показаны первые " + MaxStatesShown });
        }
    }
}

