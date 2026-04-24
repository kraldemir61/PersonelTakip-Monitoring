namespace PersonelTakip.Models;

public class StatItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public System.Collections.ObjectModel.ObservableCollection<StatItem>? SubItems { get; set; }
}
