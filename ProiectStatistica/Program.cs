
using System.Text.Json.Serialization;

public class Node
{
	[JsonPropertyName("message")]
	public Message Message { get; set; }

	[JsonPropertyName("children")]
	public List<string> Children { get; set; }
}

public class Message
{
	[JsonPropertyName("author")]
	public Author Author { get; set; }

	[JsonPropertyName("content")]
	public Content Content { get; set; }
}

public class Author
{
	[JsonPropertyName("role")]
	public string Role { get; set; } // "user" or "assistant"
}

public class Content
{
	[JsonPropertyName("parts")]
	public List<string> Parts { get; set; }
}

public class Conversation {
	public Dictionary<string, Node> Mapping { get; set; }
}