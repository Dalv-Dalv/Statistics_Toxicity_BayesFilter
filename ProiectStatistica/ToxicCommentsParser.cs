using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

public static class ToxicCommentsParser {
	public static (string message, int[] typesOfToxicity)[] ParseCsv(string path) {
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

		using (var reader = new StreamReader(path))
		using (var csv = new CsvReader(reader, config)) {
			csv.Read();
			while (csv.Read())
				try {
					string comment = csv.GetField<string>(1) ?? string.Empty;
					int toxic = csv.GetField<int>(2);
					int severe = csv.GetField<int>(3);
					int obscene = csv.GetField<int>(4);
					int threat = csv.GetField<int>(5);
					int insult = csv.GetField<int>(6);
					int identityHate = csv.GetField<int>(7);

					var toxicityTypes = new List<int>();
					
					if(toxic != 0) toxicityTypes.Add(0); 
					if(severe != 0) toxicityTypes.Add(1); 
					if(obscene != 0) toxicityTypes.Add(2); 
					if(threat != 0) toxicityTypes.Add(3); 
					if(insult != 0) toxicityTypes.Add(4); 
					if(identityHate != 0) toxicityTypes.Add(5); 
					
					results.Add((comment, toxicityTypes.ToArray())!);
				} catch(Exception e) {
					throw e;
				}
		}
		return results.ToArray();
	}
}