#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Models.Media;

    [Table("ApiUrls")]
    [Index(nameof(Url), IsUnique = true)]
    [Index(nameof(PathPrefix))]
    public sealed class ApiUrl : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string PathPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to UrlParams (one-to-many, optional).
    /// Contains URL parameters as key-value pairs for this API URL.
    /// </summary>
    public List<BiblePublications.UrlParam> UrlParams { get; set; } = [];

    public int CompareTo(object? obj) => obj is not ApiUrl other ? 1 : Url.CompareTo(other.Url);
}
