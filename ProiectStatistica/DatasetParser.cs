using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

public static class DatasetParser {
	public static ((string message, int[] types)[] rows, string[] categoryHeaders) ParseCsv(string path) {
		var results = new List<(string, int[])>();

		var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
			HasHeaderRecord = true,
			IgnoreBlankLines = true,
			BadDataFound = null,
			DetectDelimiter = true,
			Quote = '"',
			MissingFieldFound = null,
			TrimOptions = TrimOptions.Trim
		};

		using var reader = new StreamReader(path);
		using var csv = new CsvReader(reader, config);

		csv.Read();
		csv.ReadHeader();

		var header = csv.HeaderRecord!;

		// id = index 0, comment_text = index 1
		// everything from index 2 onward are category columns
		var categoryHeaders = header.Skip(2).ToArray();
		var categoryCount = categoryHeaders.Length;

		while (csv.Read()) {
			var comment = csv.GetField<string>(1) ?? string.Empty;

			// get all category flags as ints
			var flags = new int[categoryCount];
			for (var i = 0; i < categoryCount; i++)
				flags[i] = csv.GetField<int>(i + 2);

			// store only categories where flag != 0
			var activeCategories = new List<int>();
			for (var i = 0; i < flags.Length; i++)
				if (flags[i] != 0)
					activeCategories.Add(i);

			results.Add((comment, activeCategories.ToArray()));
		}

		return (results.ToArray(), categoryHeaders);
	}
}