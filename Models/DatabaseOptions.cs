using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLegacyMigrationMCP.Models
{
	public class DatabaseOptions
	{
		public string ConnectionString { get; set; } = string.Empty;
		public string Provider { get; set; } = "PostgreSQL";
	}
}