using System.Collections.Concurrent;

namespace Converter;

public class CurrencyConverter : ICurrencyConverter
{
    private static readonly object PadLock = new object();

    private static readonly ConcurrentDictionary<string, double> CurrencyMatrix =
        new ConcurrentDictionary<string, double>();

    private static readonly Dictionary<string, int> Currency =
        new Dictionary<string, int>();

    public void ClearConfiguration()
    {
        lock (PadLock)
        {
            Currency.Clear();
            CurrencyMatrix.Clear();
        }
    }

    public void UpdateConfiguration(IEnumerable<Tuple<string, string, double>> conversionRates)
    {
        // if use async code,alternative is semaphoreSlim
        lock (PadLock)
        {
            foreach (var (source, destination, rate) in conversionRates)
            {
                Currency.TryAdd(source, Currency.Count + 1);
                Currency.TryAdd(destination, Currency.Count + 1);

                var sourceNumber = Currency.GetValueOrDefault(source);
                var destinationNumber = Currency.GetValueOrDefault(destination);
                CurrencyMatrix.AddOrUpdate($"{sourceNumber}-{destinationNumber}"
                    , rate
                    , (key, oldValue) => rate);
                CurrencyMatrix.AddOrUpdate($"{sourceNumber}-{destinationNumber}"
                    , rate
                    , (key, oldValue) => (1 / rate));
            }
        }
    }

    public double Convert(string fromCurrency, string toCurrency, double amount)
    {
        if (!Currency.ContainsKey(fromCurrency) || !Currency.ContainsKey(toCurrency))
            throw new CurrencyNotExistException();

        var fromCurrencyNum = Currency[fromCurrency];
        var toCurrencyNum = Currency[toCurrency];

        lock (PadLock)
        {
            if (CurrencyMatrix.ContainsKey($"{fromCurrencyNum}-{toCurrencyNum}"))
                return amount * CurrencyMatrix[$"{fromCurrencyNum}-{toCurrencyNum}"];

            else if (CurrencyMatrix.ContainsKey($"{toCurrencyNum}-{fromCurrencyNum}"))
                return amount * CurrencyMatrix[$"{toCurrencyNum}-{fromCurrencyNum}"];
            else
                return amount * ConversionPath(CurrencyMatrix, Currency.Count, fromCurrencyNum, toCurrencyNum, 0);
        }
    }

    private double ConversionPath(ConcurrentDictionary<string, double> currencyMatrix, int currencyCount
        , int fromCurrency, int toCurrency, int z)
    {
        var min = double.MaxValue;
        var arr = new double[currencyCount];
        if (toCurrency == fromCurrency) return 0;
        if (z >= currencyCount - 1) return double.MaxValue;

        for (var i = 0; i < currencyCount; i++)
        {
            if (currencyMatrix[$"{i}-{toCurrency}"] != 0
                && currencyMatrix[$"{i}-{toCurrency}"] != double.MaxValue)
            {
                arr[i] = currencyMatrix[$"{i}-{toCurrency}"] *
                         ConversionPath(currencyMatrix, currencyCount, fromCurrency, i, z + 1);
                if (min > arr[i] && arr[i] > 0)
                    min = arr[i];
            }
        }

        return min;
    }
}
