using System.Text.RegularExpressions;
using ProiectStatistica;


var model = new ToxicityBayesModel(@"train.csv", useBigrams: true);

var testRes = model.RunTestDataset(@"train.csv");
Console.WriteLine($"Total accuracy of {testRes.totalAccuracy*100:0.000}%");
Console.WriteLine("Category specific accuracy:");
for (int l = 0; l < ToxicityBayesModel.ToxicityLabels.Length; l++) {
	Console.WriteLine($"{ToxicityBayesModel.ToxicityLabels[l]}: {testRes.categorySpecificAccuracy[l]*100:0.000}%");
}
Console.WriteLine($"Neutral: {testRes.neutralAccuracy*100:0.000}%");

string? message;
while (!string.IsNullOrEmpty(message = Console.ReadLine())) {
	bool useDebug = message[0] == '#';
	
	var res = model.CheckMessage(message, useDebug);

	if (res.Length == 0) {
		Console.WriteLine($"Your message is neutral\n");
	} else {
		string labels = string.Join(", ", res.Select(x => ToxicityBayesModel.ToxicityLabels[x]));
		Console.WriteLine($"Your message is {labels.ToUpper()}!\n");
	}
}

Console.ReadLine();