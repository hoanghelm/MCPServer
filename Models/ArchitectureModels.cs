using System.Collections.Generic;

namespace CSharpLegacyMigrationMCP.Models
{
	public class ProjectArchitecture
	{
		public string ProjectPath { get; set; } = string.Empty;
		public bool HasExistingDataLayer { get; set; }
		public bool HasExistingBusinessLayer { get; set; }
		public bool UsesDataSets { get; set; }
		public bool UsesTableAdapters { get; set; }
		public ExtractionStrategy ExtractionStrategy { get; set; }
		
		public List<DataSetInfo> DataSetFiles { get; set; } = new List<DataSetInfo>();
		public List<DataAccessInfo> ExistingDataAccessFiles { get; set; } = new List<DataAccessInfo>();
		public List<BusinessLogicInfo> ExistingBusinessLogicFiles { get; set; } = new List<BusinessLogicInfo>();
		public List<WebFormInfo> WebFormFiles { get; set; } = new List<WebFormInfo>();
		public List<string> ExistingLayerFolders { get; set; } = new List<string>();
	}

	public class DataSetInfo
	{
		public string FilePath { get; set; } = string.Empty;
		public List<string> TableAdapters { get; set; } = new List<string>();
		public List<string> DataTables { get; set; } = new List<string>();
	}

	public class DataAccessInfo
	{
		public string FilePath { get; set; } = string.Empty;
		public string Pattern { get; set; } = string.Empty; // EntityFramework, ADO.NET, DataSet, Repository, etc.
		public List<string> DatabaseOperations { get; set; } = new List<string>();
		public List<string> ConnectionStrings { get; set; } = new List<string>();
	}

	public class BusinessLogicInfo
	{
		public string FilePath { get; set; } = string.Empty;
		public string Pattern { get; set; } = string.Empty; // Service Layer, Manager Pattern, etc.
		public List<string> BusinessMethods { get; set; } = new List<string>();
		public List<string> ValidationMethods { get; set; } = new List<string>();
	}

	public class WebFormInfo
	{
		public string FilePath { get; set; } = string.Empty;
		public bool HasDataAccess { get; set; }
		public bool HasBusinessLogic { get; set; }
		public List<string> ExtractableLogic { get; set; } = new List<string>();
		public List<string> DatabaseOperations { get; set; } = new List<string>();
		public List<string> BusinessOperations { get; set; } = new List<string>();
	}

	public class WebFormAnalysis
	{
		public bool HasDataAccess { get; set; }
		public bool HasBusinessLogic { get; set; }
		public List<string> ExtractableLogic { get; set; } = new List<string>();
		public List<string> DatabaseOperations { get; set; } = new List<string>();
		public List<string> BusinessOperations { get; set; } = new List<string>();
	}

	public enum ExtractionStrategy
	{
		ReuseAndEnhance,    // Already has good DAL/BAL separation - reuse and modernize
		ModernizeDataSets,  // Uses DataSets/TableAdapters - modernize to proper DAL
		ExtendExisting,     // Has partial separation - extend what exists
		FullExtraction      // All logic in WebForms - need complete extraction
	}
}