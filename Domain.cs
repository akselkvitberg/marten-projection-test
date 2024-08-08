using Marten.Events.Aggregation;

namespace MartenProjectionDuplicateDetection;

public class BoxAggregate
{
    public required string Id { get; set; }
    public List<int> Items {get;set;} = new();
    public void Apply(AddItem evt) {
        Items.Add(evt.Id);
    }
    
    public int Duplicates => 
        Items.GroupBy(x => x)
             .Where(x => x.Count() > 1)
             .Sum(x => x.Count() - 1);
}

public record AddItem(int Id);

public class BoxSimple
{
    public string Id { get; set; }

    public int Duplicates { get; set; }
    
    public int Items { get; set; }

}

public class BoxFull
{
    public string Id { get; set; }
    
    public List<int> Items {get;set;} = new();
    public void Apply(AddItem evt) {
        Items.Add(evt.Id);
    }
    
    public int Duplicates => 
        Items.GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Sum(x => x.Count() - 1);
}

public class BoxSimpleProjection : SingleStreamProjection<BoxSimple>
{
    public void Apply(AddItem @event, BoxSimple box)
    {
        box.Items++;
        // How do I update Duplicate count based on BoxAggregate?
    }
}

public class BoxFullProjection : SingleStreamProjection<BoxFull>
{
    public void Apply(AddItem evt, BoxFull box)
    {
        box.Items.Add(evt.Id);
    }
}