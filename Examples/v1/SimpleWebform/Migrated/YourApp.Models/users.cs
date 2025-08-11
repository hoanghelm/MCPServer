using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagement.Models
{
    /// <summary>
    /// Entity model for users table
    /// </summary>
    [Table("users")]
    public class users
    {
        [Key]
        public int Id { get; set; }

    }
}
