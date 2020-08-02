using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Xml.Serialization;

namespace ExchangeRateUpdater
{
	public class ExchangeRateProvider
	{
		private const string url = "https://www.cnb.cz/cs/financni_trhy/devizovy_trh/kurzy_devizoveho_trhu/denni_kurz.xml";
		private const string query = "?date={0:dd.MM.yyyy}";
		private static HttpClient httpClient = new HttpClient();
		private static readonly ConcurrentDictionary<DateTime, BankRates> cache = new ConcurrentDictionary<DateTime, BankRates>();

		[XmlRoot("kurzy")]
		public class BankRates
		{
			[XmlAttribute("banka")]
			public string Bank { get; set; }
			[XmlAttribute("datum")]
			public string DateRaw { get; set; }
			[XmlAttribute("poradi")]
			public int Order { get; set; }
			[XmlElement("tabulka", IsNullable = false)]
			public Table Table { get; set; }
			[XmlIgnore]
			public DateTime Date => DateTime.Parse(DateRaw, CultureInfo.GetCultureInfo("cs-CZ"));
		}

		[XmlRoot("tabulka")]
		public class Table
		{
			[XmlAttribute("typ")]
			public string Type;
			[XmlElement("radek", IsNullable = false)]
			public Row[] Rows { get; set; }
		}

		public class Row
		{
			[XmlAttribute("kod")]
			public string Code { get; set; }

			[XmlAttribute("mnozstvi")]
			public uint Volume { get; set; }

			[XmlAttribute("kurz")]
			public string RateRaw { get; set; }
			[XmlIgnore]
			public decimal Rate => decimal.Parse(RateRaw, CultureInfo.GetCultureInfo("cs-CZ"));
		}
		/// <summary>
		/// Should return exchange rates among the specified currencies that are defined by the source. But only those defined
		/// by the source, do not return calculated exchange rates. E.g. if the source contains "CZK/USD" but not "USD/CZK",
		/// do not return exchange rate "USD/CZK" with value calculated as 1 / "CZK/USD". If the source does not provide
		/// some of the currencies, ignore them.
		/// </summary>
		public IEnumerable<ExchangeRate> GetExchangeRates(IEnumerable<Currency> currencies)
		{
			var bankRates = GetBankRates();
			
			foreach (var rate in bankRates.Table.Rows.Where(f => currencies.Any(g => g.Code.Equals(f.Code, StringComparison.OrdinalIgnoreCase))))
			{
				yield return new ExchangeRate(new Currency("CZK"), new Currency(rate.Code), rate.Rate / rate.Volume);
			}
		}

		private BankRates GetBankRates()
		{
			return cache.GetOrAdd(DateTime.Today, _ =>
			{
				using (Stream stream = httpClient.GetStreamAsync(url).Result)
				{
					XmlSerializer serializer = new XmlSerializer(typeof(BankRates));
					return (BankRates)serializer.Deserialize(stream);
				}
			});		
		}
	}
}
