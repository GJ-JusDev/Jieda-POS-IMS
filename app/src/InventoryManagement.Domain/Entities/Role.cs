using System;
using System.Collections.Generic;
namespace InventoryManagement.Domain.Entities;
public class Role {
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
