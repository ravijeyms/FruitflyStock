public class FlyStock
{
	public string Label { get; set; } = "";
	public string ShortIdentifier { get; set; } = "";
	public string Genotype { get; set; } = "";
	public string X { get; set; } = "";
	public string Y { get; set; } = "";
	public string C2a { get; set; } = "";
	public string C2b { get; set; } = "";
	public string C3a { get; set; } = "";
	public string C3b { get; set; } = "";
	public string ExtraInfo { get; set; } = "";
	public string Mod { get; set; } = "";

	public List<string> Attributes { get; set; } = new();

	public void BuildAttributes()
	{
		var properties = new[]
		{
		Label,
		ShortIdentifier,
		Genotype,
		X,
		Y,
		C2a,
		C2b,
		C3a,
		C3b,
		ExtraInfo,
		Mod
	};

		Attributes = properties
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.SelectMany(x => x.Split(
				new[] { ' ', ',', ';' },
				StringSplitOptions.RemoveEmptyEntries))
			.Select(x => x.Trim())
			.Where(x => x.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
	}
}