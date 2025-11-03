using System.Text.RegularExpressions;





Console.WriteLine("Reading and processing data...");
var data = ReadCSV(@"C:\Dalv\School\University\Classes\Semestrul3\PS\Proiect\toxicity.csv");

int totalMessagesCount = data.Length;
int toxicMessagesCount = 0;

Dictionary<string, int> toxicWordAppearances = new();
Dictionary<string, int> neutralWordAppearances = new();
Dictionary<string, int> wordAppearances = new();

HashSet<string> ignoredWords = ["you", "u", "the", "are", "i", "to", "in", "for", "an", "so"];

foreach (var (msg, isToxic) in data) {
	if (isToxic) toxicMessagesCount++;

	var words = msg.Split(" ");

	for (int i = 0; i < words.Length - 1; i++) {
		string word = words[i], compoundWord = words[i] + " " + words[i + 1];
		
		AddToken(compoundWord, isToxic);
		if(ignoredWords.Contains(word)) continue;
		AddToken(word, isToxic);
	}
	
	if(!ignoredWords.Contains(words[^1])) AddToken(words[^1], isToxic);
}

var totalToxicWords = toxicWordAppearances.Values.Sum();
var totalNeutralWords = neutralWordAppearances.Values.Sum();
var vocabularySize = wordAppearances.Count;

double pToxic = (double)toxicMessagesCount / totalMessagesCount;
double pNeutral = 1.0d - pToxic;
const double alpha = 1.0;

Console.WriteLine("Finished, you can now input messages:");


string? message;
while (!string.IsNullOrEmpty(message = Console.ReadLine())) {
	Console.WriteLine($"Your message is {(IsMessageToxic(message) ? "TOXIC" : "neutral")}\n");
}








return;
string CleanMessage(string input)  {
	if (string.IsNullOrWhiteSpace(input))
		return string.Empty;

	input = input.ToLowerInvariant();
	input = Regex.Replace(input, @"[^a-z0-9?!\s]", "");
	input = Regex.Replace(input, @"\s+", " ");
	input = Regex.Replace(input, @"[!?]{2,}", match => {
		var s = match.Value;
		if (s.Contains('?') && s.Contains('!'))
			return "?!";
		if (s.Contains('?'))
			return "?";
		return "!";
	});
	input = input.Trim();

	return input;
}


(string, bool)[] ReadCSV(string path) {
	string[] lines = File.ReadAllLines(path);
	
	var data = new (string, bool)[lines.Length];

	for (int i = 0; i < lines.Length; i++) {
		var msg = lines[i];
		msg = msg[(msg.LastIndexOf(',',  msg.Length - 4) + 1)..^2];
		msg = CleanMessage(msg);
		
		bool isToxic = lines[i][^1] != '0';

		data[i] = (msg, isToxic);
	}

	return data;
}


double CalculateWordProbability(string word, bool isToxic) { // P(word|toxic)
	if (isToxic) {
		toxicWordAppearances.TryGetValue(word, out var wordCount);
		return (wordCount + alpha) / (totalToxicWords + alpha * vocabularySize);
	} else {
		neutralWordAppearances.TryGetValue(word, out var wordCount);
		return (wordCount + alpha) / (totalNeutralWords + alpha * vocabularySize);
	}
}


bool IsMessageToxic(string message) {
	string[] words = CleanMessage(message).Split();

	double logScoreToxic = Math.Log(pToxic);
	double logScoreNeutral = Math.Log(pNeutral);

	Console.ForegroundColor = ConsoleColor.DarkGray;

	Console.WriteLine($"P(toxic) = {pToxic * 100:0.00}%");
	Console.WriteLine($"P(neutral) = {pNeutral * 100:0.00}%");

	Console.WriteLine($"Current scores: Toxicity {logScoreToxic:0.00}   Neutral {logScoreNeutral:0.00}");
	
	
	for (var i = 0; i < words.Length - 1; i++) {
		string word = words[i], compoundWord = words[i] + " " + words[i + 1];
		
		logScoreToxic += Math.Log(CalculateWordProbability(compoundWord, isToxic: true));
		logScoreNeutral += Math.Log(CalculateWordProbability(compoundWord, isToxic: false));

		Console.WriteLine($"P({compoundWord}|toxic) = {CalculateWordProbability(compoundWord, isToxic: true) * 100:0.00}%");
		Console.WriteLine($"P({compoundWord}|neutral) = {CalculateWordProbability(compoundWord, isToxic: false) * 100:0.00}%");
		Console.WriteLine($"Current scores: Toxicity {logScoreToxic:0.00}   Neutral {logScoreNeutral:0.00}");
		
		if (ignoredWords.Contains(word)) continue;


		logScoreToxic += Math.Log(CalculateWordProbability(word, isToxic: true));
		logScoreNeutral += Math.Log(CalculateWordProbability(word, isToxic: false));
		
		Console.WriteLine($"P({word}|toxic) = {CalculateWordProbability(word, isToxic: true) * 100:0.00}%");
		Console.WriteLine($"P({word}|neutral) = {CalculateWordProbability(word, isToxic: false) * 100:0.00}%");
		Console.WriteLine($"Current scores: Toxicity {logScoreToxic:0.00}   Neutral {logScoreNeutral:0.00}");
	}

	if (!ignoredWords.Contains(words[^1])) {
		logScoreToxic += Math.Log(CalculateWordProbability(words[^1], isToxic: true));
		logScoreNeutral += Math.Log(CalculateWordProbability(words[^1], isToxic: false));
		Console.WriteLine($"P({words[^1]}|toxic) = {CalculateWordProbability(words[^1], isToxic: true) * 100:0.00}%");
		Console.WriteLine($"P({words[^1]}|neutral) = {CalculateWordProbability(words[^1], isToxic: false) * 100:0.00}%");
		Console.WriteLine($"Current scores: Toxicity {logScoreToxic:0.00}   Neutral {logScoreNeutral:0.00}");
	}

	Console.WriteLine($"Toxic score: {logScoreToxic:0.00}  Neutral Score: {logScoreNeutral:0.00}");
	
	
	Console.ForegroundColor = ConsoleColor.Gray;

	
	return logScoreToxic > logScoreNeutral;
}

void AddToken(string word, bool isToxic) {
	if(!wordAppearances.TryAdd(word, 1)) wordAppearances[word]++;
		
	if (isToxic) {
		if(!toxicWordAppearances.TryAdd(word, 1)) toxicWordAppearances[word]++;
	} else {
		if(!neutralWordAppearances.TryAdd(word, 1)) neutralWordAppearances[word]++;
	}
}