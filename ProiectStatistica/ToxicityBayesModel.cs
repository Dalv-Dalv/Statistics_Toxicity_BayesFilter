using System.Text.RegularExpressions;

namespace ProiectStatistica;

public class ToxicityBayesModel {
	public readonly string[] toxicityLabels;
	private readonly Dictionary<string, int>[] toxicWordAppearances;
	private readonly Dictionary<string, int>[] nonToxicWordAppearances;
	private readonly int[] totalToxicWordsSum;    // Numarul total de cuvinte in mesaje toxice 
	private readonly int[] totalNonToxicWordsSum; // Numarul total de cuvinte in mesaje care nu au un anumit tip de toxicitate
	private int neutralWordsSum = 0;
	
	private readonly Dictionary<string, int> neutralWordAppearances = new();
	private readonly Dictionary<string, int> totalWordAppearances = new(); 

	// Probabilitati a priori
	private readonly double[] toxicityProbabilities; 
	private double neutralProbability = 0; 

	private readonly HashSet<string> ignoredWords = [];

	// Daca sa consideram si grupari de genul "cuv1 cuv2" intr-un singur token
	private readonly bool useBigrams;
	
	public ToxicityBayesModel(string path, bool useBigrams = true, double trainPercentage = 1.0d) {
		this.useBigrams = useBigrams;


		Console.ForegroundColor = ConsoleColor.DarkGray;
		Console.WriteLine("Reading and parsing data...");
		
		var allData = DatasetParser.ParseCsv(path);
		var data = allData.rows;
		int trainingLimit = (int) Math.Floor(data.Length * Math.Clamp(trainPercentage, 0.0, 1.0));
		data = data[..trainingLimit];

		toxicityLabels = allData.categoryHeaders.Select(CleanCategory).ToArray();
		int nrCategories = toxicityLabels.Length;
		toxicWordAppearances = new Dictionary<string, int>[nrCategories];
		nonToxicWordAppearances = new Dictionary<string, int>[nrCategories];
		totalToxicWordsSum = new int[nrCategories];
		totalNonToxicWordsSum = new int[nrCategories];
		toxicityProbabilities = new double[nrCategories];
		
		
		Console.WriteLine($"Data has been read ({data.Length} entries), preprocessing...");
		
		Console.Write("   Cleaning input messages... ");
		data = data.Select(tup => (CleanMessage(tup.message), tup.types)).ToArray();
		Console.WriteLine("Done");

		Console.Write("   Populating internal variables... ");
		PopulateDictionaries(data);
		Console.WriteLine("Done");

		Console.WriteLine("   Removing rare words...");
		RemoveRareWords(1);
		// AddUselessWordsToIgnoredSet();

		Console.WriteLine($"Ignored a total of {ignoredWords.Count} words out of {totalWordAppearances.Count} ({(double)ignoredWords.Count / totalWordAppearances.Count * 100:0.00}%), words remaining: {totalWordAppearances.Count - ignoredWords.Count}");
		
		UpdateProbabilities(data);

		Console.WriteLine("Finished processing");
		
		Console.ForegroundColor = ConsoleColor.Gray;
	}

	private void PopulateDictionaries((string, int[])[] data) {
		// Resetam dictionarele
		for (int i = 0; i < toxicityLabels.Length; i++) {
			toxicWordAppearances[i] ??= new Dictionary<string, int>(); 
			toxicWordAppearances[i].Clear();

			nonToxicWordAppearances[i] ??= new  Dictionary<string, int>(); 
			nonToxicWordAppearances[i].Clear();
		}
		neutralWordAppearances.Clear();
		totalWordAppearances.Clear();
		
		// Iteram prin fiecare mesaj
		foreach (var (message, types) in data) {
			var words = message.Split(' ');

			// Iteram prin fiecare cuvant
			for (int i = 0; i < words.Length; i++) {
				var word = words[i];
				if(!ignoredWords.Contains(word)) AddToken(word, types);

				if (i >= words.Length - 1 || !useBigrams) continue;

				word = words[i] + " " + words[i + 1];
				if(!ignoredWords.Contains(word)) AddToken(word, types);
			}
		}

		// Dupa ce au fost populate, calculam sumele (numarul total de cuvinte) pentru a calcula prioritatile posteriori cand verificam cuvinte
		for (int l = 0; l < toxicityLabels.Length; l++) {
			totalToxicWordsSum[l] = toxicWordAppearances[l].Values.Sum();
			totalNonToxicWordsSum[l] = nonToxicWordAppearances[l].Values.Sum();
		}

		neutralWordsSum = neutralWordAppearances.Values.Sum();
	}

	// Adauga token in dictionarele corespunzatoare
	private void AddToken(string token, int[] toxicityTypes) {
		if (!totalWordAppearances.TryAdd(token, 1)) totalWordAppearances[token]++;

		if (toxicityTypes.Length == 0) {
			if (!neutralWordAppearances.TryAdd(token, 1)) neutralWordAppearances[token]++;
		} else {
			for (int i = 0; i < toxicityLabels.Length; i++) {
				if (toxicityTypes.Contains(i)) {
					if (!toxicWordAppearances[i].TryAdd(token, 1)) toxicWordAppearances[i][token]++;
				} else {
					if (!nonToxicWordAppearances[i].TryAdd(token, 1)) nonToxicWordAppearances[i][token]++;
				}
			}
		}
	}

	
	// Sterge cuvinte ce apar de <= minFrequency ori
	private void RemoveRareWords(int minFrequency = 3) {
		var rareWords = totalWordAppearances
		                .Where(kv => kv.Value <= minFrequency)
		                .Select(kv => kv.Key)
		                .ToHashSet();

		foreach (var word in rareWords) ignoredWords.Add(word);

		Console.WriteLine($"Removed {rareWords.Count} ({(double)rareWords.Count / totalWordAppearances.Count * 100:0.00}%) rare words (appeared <= {minFrequency} times)");
	}
	
	
	// Cauta cuvinte ce au o probabilitate foarte apropiata de a fi in multe categorii
	private void AddUselessWordsToIgnoredSet() {
		const double uselessnessThreshold = 0.00001;

		int total = totalWordAppearances.Count;
		int removed = 0;
		Console.WriteLine($"Analyzing useless words... (total of {totalWordAppearances.Count} words)");
		
		foreach (var word in totalWordAppearances.Keys) {
			if (ignoredWords.Contains(word)) continue;
			
			var probs = new double[toxicityLabels.Length + 1];
			for (var i = 0; i < toxicityLabels.Length; i++)
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

	
	// Updateaza probabilitatile a priori
	private void UpdateProbabilities((string, int[])[] data) {
		int[] categorySpecific = new int[toxicityLabels.Length];
		for (int i = 0; i < toxicityLabels.Length; i++) categorySpecific[i] = 0;

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
			Console.WriteLine($"{toxicityLabels[i]} {toxicityProbabilities[i]*100:0.000}%");
		}
		
		neutralProbability = (double)totalNeutral / data.Length;
		Console.WriteLine($"Neutral: {neutralProbability*100:0.000}%");
		
		// Test to see if perfectly balanced probabilities gives better results on tests, and it marginally improves tests :/ ...
		// for (int i = 0; i < ToxicityLabels.Length; i++)
		// 	toxicityProbabilities[i] = 1.0 / ToxicityLabels.Length;
		// neutralProbability = 1.0 / (ToxicityLabels.Length + 1);
	}
	
	public int[] CheckMessage(string message, bool debug=false) {
		Console.ForegroundColor = ConsoleColor.DarkGray;
		
		var words = CleanMessage(message).Split();
		
		double[] logScores =  new double[toxicityProbabilities.Length];
		double[] logNonScores = new double[toxicityProbabilities.Length];
		
		for (int l = 0; l < toxicityProbabilities.Length; l++) {
			logScores[l] = Math.Log(toxicityProbabilities[l]);
			logNonScores[l] = Math.Log(1.0d - toxicityProbabilities[l]);
			
			if(debug) Console.WriteLine($"Current scores: {toxicityLabels[l]} {logScores[l]:0.00} ({toxicityProbabilities[l]*100:0.00}%)   Non-{toxicityLabels[l].ToLower()} {logNonScores[l]:0.00} ({(1.0d - toxicityProbabilities[l])*100:0.00}%)");
		}


		for (var i = 0; i < words.Length; i++) {
			var word = words[i];

			if (!ignoredWords.Contains(word)) {
				for (int l = 0; l < logScores.Length; l++) {
					logScores[l] += Math.Log(CalculateWordProbability(word, l, false));
					logNonScores[l] += Math.Log(CalculateWordProbability(word, l, true));

					if (debug) {
						Console.WriteLine($"P({word}|{toxicityLabels[l]}) = {CalculateWordProbability(word, l, false) * 100:0.00}%");
						Console.WriteLine($"P({word}|not {toxicityLabels[l]}) = {CalculateWordProbability(word, l, true) * 100:0.00}%");
						Console.WriteLine($"Current scores: {toxicityLabels[l]} {logScores[l]:0.00}   non-{toxicityLabels[l].ToLower()} {logNonScores[l]:0.00}\n");	
					}
				}
			}
			
			if (i >= words.Length - 1 || !useBigrams) continue;

			word = words[i] + " " + words[i + 1];

			if (ignoredWords.Contains(word)) continue;
			
			for (int l = 0; l < logScores.Length; l++) {
				logScores[l] += Math.Log(CalculateWordProbability(word, l, false));
				logNonScores[l] += Math.Log(CalculateWordProbability(word, l, true));

				if (!debug) continue;
				Console.WriteLine($"P({word}|{toxicityLabels[l]}) = {CalculateWordProbability(word, l, false) * 100:0.00}%");
				Console.WriteLine($"P({word}|not {toxicityLabels[l]}) = {CalculateWordProbability(word, l, true) * 100:0.00}%");
				Console.WriteLine($"Current scores: {toxicityLabels[l]} {logScores[l]:0.00}   non-{toxicityLabels[l].ToLower()} {logNonScores[l]:0.00}\n");
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
	public void RunTestDataset(string path, bool useDebug = false, double testPercentage = 1.0) {
	    Console.ForegroundColor = ConsoleColor.DarkGray;
	    Console.WriteLine("Reading and parsing test data...");
	    
	    var data = DatasetParser.ParseCsv(path).rows;
	    int trainingLimit = (int) Math.Floor(data.Length * Math.Clamp(testPercentage, 0.0, 1.0));
	    data = data[^trainingLimit..]; // Take last trainingLimit elements

	    Console.WriteLine("Test data has been read, preprocessing...");
	    data = data.Select(tup => (CleanMessage(tup.message), tup.types)).ToArray();

	    Console.WriteLine($"Finished preprocessing, running {data.Length} tests...");

	    Console.ForegroundColor = ConsoleColor.DarkGray;

	    int totalCategories = toxicityLabels.Length;

	    int totalCorrectGeneral = 0;
	    int totalCorrect = 0;

	    int[] tp = new int[totalCategories]; // True positives
	    int[] tn = new int[totalCategories]; // True negatives
	    int[] fp = new int[totalCategories]; // False positives
	    int[] fn = new int[totalCategories]; // False negatives
	    int[] categorizedTotals = new int[totalCategories];

	    for (int i = 0; i < data.Length; i++) {
	        var res = CheckMessage(data[i].message);
	        var actual = data[i].types;

	        if (res.Length > 0 && actual.Length > 0) totalCorrectGeneral++;

	        for (int c = 0; c < totalCategories; c++) {
	            bool isActual = actual.Contains(c);
	            bool isPredicted = res.Contains(c);

	            if (isActual && isPredicted) tp[c]++;
	            else if (!isActual && !isPredicted) tn[c]++;
	            else if (!isActual && isPredicted) fp[c]++;
	            else if (isActual && !isPredicted) fn[c]++;
	        }

	        foreach (var l in actual) {
	            categorizedTotals[l]++;
	            if (res.Contains(l)) totalCorrect++;
	        }

	        if (!useDebug) continue;
	        Console.ForegroundColor = ConsoleColor.DarkGray;
	        Console.WriteLine($"Test text:\n  \"{data[i].message}\"\n  Result: [{string.Join(", ", res.Select(x => toxicityLabels[x]))}]");
	        Console.WriteLine($"Expected: [{string.Join(", ", actual.Select(x => toxicityLabels[x]))}]\n");
	    }

	    Console.ForegroundColor = ConsoleColor.Gray;

	    Console.WriteLine($"Neutral accuracy (any toxicity predicted correctly): {(double)totalCorrectGeneral / data.Length*100:0.00}%");
	    Console.WriteLine($"Total accuracy (all categories individually correct): {(double)totalCorrect / (data.Length * totalCategories)*100:0.00}%");

	    Console.WriteLine("\nCategory-specific accuracy and counts");
	    for (int i = 0; i < totalCategories; i++) {
	        double precision = tp[i] + fp[i] > 0 ? (double)tp[i] / (tp[i] + fp[i]) : 0;
	        double recall = tp[i] + fn[i] > 0 ? (double)tp[i] / (tp[i] + fn[i]) : 0;
	        double f1 = precision + recall > 0 ? 2 * (precision * recall) / (precision + recall) : 0;
	        double accuracy = (double)(tp[i] + tn[i]) / (tp[i] + tn[i] + fp[i] + fn[i]);

	        Console.WriteLine($"{toxicityLabels[i]}:");
	        Console.WriteLine($"  Accuracy: {accuracy:P2}");
	        Console.WriteLine($"  Precision: {precision:P2}, Recall: {recall:P2}, F1 Score: {f1:P2}");
	        Console.WriteLine($"  (Counts: TP {tp[i], -4} TN {tn[i], -4} FP: {fp[i], -4} FN: {fn[i], -4})\n");
	    }
	}

	
	
	
	
	
	
	private static string CleanMessage(string input) {
		if (string.IsNullOrWhiteSpace(input))
			return string.Empty;

		input = input.ToLowerInvariant();
		input = Regex.Replace(input, @"[,:.?!]", " ");
		input = Regex.Replace(input, @"[^a-z\s]", "");
		input = Regex.Replace(input, @"\s+", " ");
		input = input.Trim();

		return input;
	}
	public static string CleanCategory(string input) {
		if (string.IsNullOrWhiteSpace(input)) return string.Empty;

		string cleaned = input.Replace("_", " ").ToLower();

		return char.ToUpper(cleaned[0]) + cleaned.Substring(1);
	}
}