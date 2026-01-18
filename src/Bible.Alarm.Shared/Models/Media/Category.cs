#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

[Table("Categories")]
public sealed class Category : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to BiblePublications in this category
    /// </summary>
    public List<BiblePublications.BiblePublication> BiblePublications { get; set; } = [];

    public int CompareTo(object? obj) => obj is not Category other ? 1 : CategoryName.CompareTo(other.CategoryName);
}
