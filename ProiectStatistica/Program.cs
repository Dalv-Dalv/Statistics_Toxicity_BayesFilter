using System.Text.RegularExpressions;
using ProiectStatistica;


Console.WriteLine("Usage:\nThe program will automatically train a naive Bayes model and run a test dataset. Afterwards you may enter custom strings in the console, putting a '#' at the start of the line will show processing details.\n");

Console.WriteLine("Press 1 to disable bigrams (Enter --> Skip):");
string query = Console.ReadLine();
bool useBigrams = string.IsNullOrEmpty(query);

Console.WriteLine("Press 1 to enable Debug mode for tests (Enter --> Skip):");
query = Console.ReadLine();
bool globalUseDebug = !string.IsNullOrEmpty(query) && int.Parse(query) == 1;

var model = new ToxicityBayesModel(@"games_esential_only.csv", useBigrams, trainPercentage:0.5);
Console.WriteLine();
model.RunTestDataset(@"games_esential_only.csv", globalUseDebug, testPercentage: 0.5);



Console.WriteLine("\n\n\nYou can now input custom messages to test:");
string? message;
while (!string.IsNullOrEmpty(message = Console.ReadLine())) {
	bool useDebug = message[0] == '#';
	
	var res = model.CheckMessage(message, useDebug);

	if (res.Length == 0) {
		Console.WriteLine($"Your message is neutral\n");
	} else {
		string labels = string.Join(", ", res.Select(x => model.toxicityLabels[x]));
		Console.WriteLine($"Your message is {labels.ToUpper()}!\n");
	}
}

Console.ReadLine();