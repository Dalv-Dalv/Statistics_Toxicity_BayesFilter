using System.Text.RegularExpressions;

namespace ProiectStatistica;

public class ToxicityBayesModel {
	public static readonly string[] ToxicityLabels = ["Toxic", "Severe", "Obscene", "Threat", "Insult", "Identity hate"];
	private readonly Dictionary<string, int>[] toxicWordAppearances = new Dictionary<string, int>[ToxicityLabels.Length];
	private readonly Dictionary<string, int>[] nonToxicWordAppearances = new Dictionary<string, int>[ToxicityLabels.Length];
	private readonly int[] totalToxicWordsSum = new int[6];
	private readonly int[] totalNonToxicWordsSum = new int[6];
	
	private readonly Dictionary<string, int> neutralWordAppearances = new();
	private int neutralWordsSum = 0;
	private readonly Dictionary<string, int> totalWordAppearances = new();

	private double[] toxicityProbabilities = new double[ToxicityLabels.Length];
	private double neutralProbability = 0;

	private readonly HashSet<string> ignoredWords = [];

	private readonly bool useBigrams;
	
	public ToxicityBayesModel(string path, bool useBigrams = true) {
		this.useBigrams = useBigrams;


		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine("Reading and parsing data...");
		
		var data = ToxicCommentsParser.ParseCsv(path);
		data = data[..150_000];

		Console.WriteLine($"Data has been read ({data.Length} entries), preprocessing...");
		data = data.Select(tup => (CleanMessage(tup.message), tup.typesOfToxicity)).ToArray();

		PopulateDictionaries(data);
		
		RemoveRareWords(1);
		// AddUselessWordsToIgnoredSet();

		Console.WriteLine($"Ignored a total of {ignoredWords.Count} words out of {totalWordAppearances.Count} ({(double)ignoredWords.Count / totalWordAppearances.Count * 100:0.00}%), words remaining: {totalWordAppearances.Count - ignoredWords.Count}");
		
		UpdateProbabilities(data);

		Console.WriteLine("Finished processing");
		
		Console.ForegroundColor = ConsoleColor.Gray;
	}

	private void PopulateDictionaries((string, int[])[] data) {
		for (int i = 0; i < ToxicityLabels.Length; i++) {
			if (toxicWordAppearances[i] == null) toxicWordAppearances[i] = new Dictionary<string, int>(); 
			else toxicWordAppearances[i].Clear();

			if (nonToxicWordAppearances[i] == null) nonToxicWordAppearances[i] = new  Dictionary<string, int>(); 
			else nonToxicWordAppearances[i].Clear();
		}
		neutralWordAppearances.Clear();
		totalWordAppearances.Clear();
		
		foreach (var (message, types) in data) {
			var words = message.Split(' ');

			for (int i = 0; i < words.Length; i++) {
				var word = words[i];
				if(!ignoredWords.Contains(word)) AddToken(word, types);

				if (i >= words.Length - 1 || !useBigrams) continue;

				word = words[i] + " " + words[i + 1];
				if(!ignoredWords.Contains(word)) AddToken(word, types);
			}
		}

		for (int l = 0; l < ToxicityLabels.Length; l++) {
			totalToxicWordsSum[l] = toxicWordAppearances[l].Values.Sum();
			totalNonToxicWordsSum[l] = nonToxicWordAppearances[l].Values.Sum();
		}

		neutralWordsSum = neutralWordAppearances.Values.Sum();
	}

	private void AddToken(string token, int[] toxicityTypes) {
		if (!totalWordAppearances.TryAdd(token, 1)) totalWordAppearances[token]++;

		if (toxicityTypes.Length == 0) {
			if (!neutralWordAppearances.TryAdd(token, 1)) neutralWordAppearances[token]++;
		} else {
			for (int i = 0; i < ToxicityLabels.Length; i++) {
				if (toxicityTypes.Contains(i)) {
					if (!toxicWordAppearances[i].TryAdd(token, 1)) toxicWordAppearances[i][token]++;
				} else {
					if (!nonToxicWordAppearances[i].TryAdd(token, 1)) nonToxicWordAppearances[i][token]++;
				}
			}
		}
	}

	private void AddUselessWordsToIgnoredSet() {
		const double uselessnessThreshold = 0.00001; // tune this empirically

		int total = totalWordAppearances.Count;
		int removed = 0;
		Console.WriteLine($"Analyzing useless words... (total of {totalWordAppearances.Count} words)");
		
		foreach (var word in totalWordAppearances.Keys) {
			if (ignoredWords.Contains(word)) continue;
			
			var probs = new double[ToxicityLabels.Length + 1];
			for (var i = 0; i < ToxicityLabels.Length; i++)
				probs[i] = CalculateWordProbability(word, i, false);
		
			probs[^1] = (neutralWordAppearances.GetValueOrDefault(word, 0) + 1.0) / (neutralWordsSum + totalWordAppearances.Count);
		
			var max = probs.Max();
			var min = probs.Min();
			var diff = max - min;
		
			if (diff >= uselessnessThreshold) continue;
			
			ignoredWords.Add(word);
			removed++;
		}
		
		Console.WriteLine($"Removed {removed} useless words (purged {(double)removed / total * 100:0.00}% of words)");
	}
	
	private void RemoveRareWords(int minFrequency = 3) {
		var rareWords = totalWordAppearances
		                .Where(kv => kv.Value <= minFrequency)
		                .Select(kv => kv.Key)
		                .ToHashSet();

		foreach (var word in rareWords) ignoredWords.Add(word);

		Console.WriteLine($"Removed {rareWords.Count} ({(double)rareWords.Count / totalWordAppearances.Count * 100:0.00}%) rare words (appeared <= {minFrequency} times)");
	}

	
	private void UpdateProbabilities((string, int[])[] data) {
		int[] categorySpecific = new int[ToxicityLabels.Length];
		for (int i = 0; i < ToxicityLabels.Length; i++) categorySpecific[i] = 0;

		int totalNeutral = 0;
		
		foreach (var (_, types) in data) {
			if (types.Length == 0) {
				totalNeutral++;
			}
			
			foreach (var type in types) {
				categorySpecific[type]++;
			}
		}

		Console.WriteLine($"Found probabilties:");
		for (int i = 0; i < categorySpecific.Length; i++) {
			toxicityProbabilities[i] = (double)categorySpecific[i] / data.Length;
			Console.WriteLine($"{ToxicityLabels[i]} {toxicityProbabilities[i]*100:0.000}%");
		}
		
		neutralProbability = (double)totalNeutral / data.Length;
		Console.WriteLine($"Neutral: {neutralProbability*100:0.000}%");
		
		// Test to see if perfectly balanced probabilities gives better results on tests, and it marginally improves tests...
		// for (int i = 0; i < ToxicityLabels.Length; i++)
		// 	toxicityProbabilities[i] = 1.0 / ToxicityLabels.Length;
		// neutralProbability = 1.0 / (ToxicityLabels.Length + 1);
	}

	private static string CleanMessage(string input) {
		if (string.IsNullOrWhiteSpace(input))
			return string.Empty;

		input = input.ToLowerInvariant();
		input = Regex.Replace(input, @"[^a-z\s]", "");
		input = Regex.Replace(input, @"\s+", " ");
		input = input.Trim();

		return input;
	}

	public int[] CheckMessage(string message, bool debug=false) {
		Console.ForegroundColor = ConsoleColor.DarkGray;
		
		
		var words = CleanMessage(message).Split();
		
		double[] logScores =  new double[toxicityProbabilities.Length];
		double[] logNonScores = new double[toxicityProbabilities.Length];
		
		for (int l = 0; l < toxicityProbabilities.Length; l++) {
			logScores[l] = Math.Log(toxicityProbabilities[l]);
			logNonScores[l] = Math.Log(1.0d - toxicityProbabilities[l]);
			
			if(debug) Console.WriteLine($"Current scores: {ToxicityLabels[l]} {logScores[l]:0.00} ({toxicityProbabilities[l]*100:0.00}%)   Non-{ToxicityLabels[l].ToLower()} {logNonScores[l]:0.00} ({(1.0d - toxicityProbabilities[l])*100:0.00}%)");
		}


		for (var i = 0; i < words.Length; i++) {
			var word = words[i];

			if (!ignoredWords.Contains(word)) {
				for (int l = 0; l < logScores.Length; l++) {
					logScores[l] += Math.Log(CalculateWordProbability(word, l, false));
					logNonScores[l] += Math.Log(CalculateWordProbability(word, l, true));

					if (debug) {
						Console.WriteLine($"P({word}|{ToxicityLabels[l]}) = {CalculateWordProbability(word, l, false) * 100:0.00}%");
						Console.WriteLine($"P({word}|not {ToxicityLabels[l]}) = {CalculateWordProbability(word, l, true) * 100:0.00}%");
						Console.WriteLine($"Current scores: {ToxicityLabels[l]} {logScores[l]:0.00}   Neutral {logNonScores[l]:0.00}\n");	
					}
				}
			}
			
			if (i >= words.Length - 1 || !useBigrams) continue;

			word = words[i] + " " + words[i + 1];
			
			if (!ignoredWords.Contains(word)) {
				for (int l = 0; l < logScores.Length; l++) {
					logScores[l] += Math.Log(CalculateWordProbability(word, l, false));
					logNonScores[l] += Math.Log(CalculateWordProbability(word, l, true));

					if(debug) {
						Console.WriteLine($"P({word}|{ToxicityLabels[l]}) = {CalculateWordProbability(word, l, false) * 100:0.00}%");
						Console.WriteLine($"P({word}|not {ToxicityLabels[l]}) = {CalculateWordProbability(word, l, true) * 100:0.00}%");
						Console.WriteLine($"Current scores: {ToxicityLabels[l]} {logScores[l]:0.00}   Neutral {logNonScores[l]:0.00}\n");
					}
				}
			}
		}
		
		List<int> labels = [];
		for (int l = 0; l < toxicityProbabilities.Length; l++) {
			if (logScores[l] < logNonScores[l]) continue;
			
			labels.Add(l);
		}
		
		Console.ForegroundColor = ConsoleColor.Gray;

		return labels.ToArray();
	}

	private double CalculateWordProbability(string word, int labelIndex, bool negate) {
		var dict = negate ? nonToxicWordAppearances[labelIndex] : toxicWordAppearances[labelIndex];
		var total = negate ? totalNonToxicWordsSum[labelIndex] : totalToxicWordsSum[labelIndex];
		
		int wordCount = dict.GetValueOrDefault(word, 0);
		int vocabSize = totalWordAppearances.Count;

		return Math.Max((wordCount + 1.0) / (total + vocabSize), 1e-6);
	}

	private double CalculateNeutralWordProbability(string word) {
		var dict = neutralWordAppearances;
		var total = neutralWordsSum;
		
		int wordCount = dict.GetValueOrDefault(word, 0);
		int vocabSize = totalWordAppearances.Count;

		return Math.Max((wordCount + 1.0) / (total + vocabSize), 1e-6);
	}

	public (double neutralAccuracy, double totalAccuracy, double[] categorySpecificAccuracy) RunTestDataset(string path) {
		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine("Reading and parsing data...");
		
		var data = ToxicCommentsParser.ParseCsv(path);
		data = data[150_000..];

		Console.WriteLine("Data has been read, preprocessing...");
		data = data.Select(tup => (CleanMessage(tup.message), tup.typesOfToxicity)).ToArray();

		Console.WriteLine($"Finished preprocessing, running {data.Length} tests...");


		Console.ForegroundColor = ConsoleColor.DarkGray;
		int total = ToxicityLabels.Length;
		int totalCorrectGeneral = 0;
		int totalCorrect = 0;
		int[] categorizedCorrect = new int[total];
		int[] categorizedTotals = new int[total];
		for (int i = 0; i < total; i++) {
			categorizedCorrect[i] = 0;
			categorizedTotals[i] = 0;
		}
		
		for (int i = 0; i < data.Length; i++) {
			var res = CheckMessage(data[i].message);
			var actual = data[i].typesOfToxicity;

			if (res.Length > 0 && actual.Length > 0) {
				totalCorrectGeneral++;
			}

			foreach (var l in actual) {
				categorizedTotals[l]++;

				if (!res.Contains(l)) continue;

				totalCorrect++;
				categorizedCorrect[l]++;
			}
			
			// Console.WriteLine($"Ran {i}/{data.Length}({(float)i / data.Length * 100:0.00}%) tests...    ({(double)totalCorrectGeneral / (i + 1)*100:0.000}%) ({(double)totalCorrect / ((i + 1) * total)*100:0.000}%) ({(double)(categorizedCorrect[0] + 1) / (categorizedTotals[0] + 1) *100:0.000}%)  ({(double)(categorizedCorrect[1] + 1) / (categorizedTotals[1] + 1)*100:0.000}%)  ({(double)(categorizedCorrect[2] + 1) / (categorizedTotals[2] + 1)*100:0.000}%)  ({(double)(categorizedCorrect[3] + 1) / (categorizedTotals[3] + 1)*100:0.000}%)  ({(double)(categorizedCorrect[4] + 1) / (categorizedTotals[4] + 1)*100:0.000}%)  ({(double)(categorizedCorrect[5] + 1) / (categorizedTotals[5] + 1)*100:0.000}%)");
		}
		
		Console.ForegroundColor = ConsoleColor.Gray;

		var categorizedResults = new double[total];
		for (int i = 0; i < total; i++) categorizedResults[i] = (double)(categorizedCorrect[i] + 1) / (categorizedTotals[i] + 1);
		
		return ((double)totalCorrectGeneral / data.Length, 
			    (double)totalCorrect / (data.Length * total),
			    categorizedResults);
	}
}