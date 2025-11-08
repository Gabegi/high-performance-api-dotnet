using MessagePack;

namespace ApexShop.API.DTOs;

/// <summary>
/// Response model for bulk create operations. Contains count and created entity IDs.
/// </summary>
[MessagePackObject(true)]
public partial class BulkCreateResult
{
    public int Count { get; set; }
    public string? Message { get; set; }
    public List<int>? ProductIds { get; set; }
}

/// <summary>
/// Generic bulk operation result for various entity types.
/// Used for bulk create/delete operations across different entities.
/// </summary>
[MessagePackObject(true)]
public partial class BulkCreateResultGeneric
{
    public int Count { get; set; }
    public string? Message { get; set; }
    public List<int>? ProductIds { get; set; }
    public List<int>? CategoryIds { get; set; }
    public List<int>? ReviewIds { get; set; }
    public List<int>? UserIds { get; set; }
    public List<int>? OrderIds { get; set; }
}
