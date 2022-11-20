﻿using MongoDB.Driver;
using QuizHouse.Dto;
using System.Linq.Expressions;
using System;
using System.Reflection.Metadata;
using System.Xml.Linq;
using MongoDB.Bson;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections;
using System.Reflection;

namespace QuizHouse.Utility
{
	public class DataTableProcessParametrs
	{
		public class SearchData
		{
			public string Value { get; set; }
		}
		public class ColumndData
		{
			public string Data { get; set; }
			public SearchData Search { get; set; }
		}

		public class OrderData
		{
			public int Column { get; set; }
			public string Dir { get; set; }
		};
		public ColumndData[] Columns { get; set; }
		public int Draw { get; set; }
		public int Start { get; set; }
		public int Length { get; set; }
		public OrderData[] Order { get; set; }
		public SearchData Search { get; set; }
	};

	internal class DatatableFiledInfo
	{
		public string Name { get; set; }
		public BsonType Type { get; set; }
		public bool IsEnumerable { get; set; }
	}

	public class DatatablesPagingDefinition<T>
	{
		private DataTableProcessParametrs _parametrs;
		private FindOptions<T> _findOptions;
		private FilterDefinition<T> _filter;

		private DatatableFiledInfo _globalSearchField;
		private List<DatatableFiledInfo> _allowedFilters;

		public DatatablesPagingDefinition(DataTableProcessParametrs parametrs)
		{
			_parametrs = parametrs;
			_allowedFilters = new List<DatatableFiledInfo>();
			_findOptions = new FindOptions<T>() { Skip = parametrs.Start, Limit = parametrs.Length };
			_filter = Builders<T>.Filter.Empty;
		}

		public FindOptions<T> GetOptions()
		{
			return _findOptions;
		}

		public FilterDefinition<T> GetFilter()
		{
			return _filter;
		}

		public DatatablesPagingDefinition<T> ApplySort()
		{
			SortDefinition<T> sort = null;

			foreach (var order in _parametrs.Order)
			{
				var columnName = GetColumnName(_parametrs.Columns[order.Column].Data);

				if (order.Dir == "asc")
					sort = sort == null ? Builders<T>.Sort.Ascending(columnName) : sort.Ascending(columnName);
				else
					sort = sort == null ? Builders<T>.Sort.Descending(columnName) : sort.Descending(columnName);
			}

			if (sort != null)
				_findOptions.Sort = sort;
			return this;
		}

		public DatatablesPagingDefinition<T> SetGlobalFilterField(Expression<Func<T, object>> field)
		{
			_globalSearchField = GetFieldInfo(field);

			return this;
		}

		public DatatablesPagingDefinition<T> AllowFilterFor(Expression<Func<T, object>> field)
		{
			_allowedFilters.Add(GetFieldInfo(field));
			return this;
		}

		public DatatablesPagingDefinition<T> ApplyColumnFilter()
		{
			foreach (var column in _parametrs.Columns)
			{
				if (column.Search == null || string.IsNullOrEmpty(column.Search.Value)) continue;
				var name = GetColumnName(column.Data);
				var fildInfo = _allowedFilters.FirstOrDefault(x => x.Name == name);
				if (fildInfo == null) continue;

				AddToFilter(fildInfo, column.Search.Value);
			}

			return this;
		}

		public DatatablesPagingDefinition<T> ApplyGlobalFilter()
		{
			if (_globalSearchField != null && _parametrs.Search != null && !string.IsNullOrEmpty(_parametrs.Search.Value))
			{
				AddToFilter(_globalSearchField, _parametrs.Search.Value);
			}
			return this;
		}
		public async Task<(List<T>, long, long)> Execute(IMongoCollection<T> collection)
		{
			var totalRecords = await collection.EstimatedDocumentCountAsync();
			var filteredRecords = totalRecords;
			if (_filter != Builders<T>.Filter.Empty)
				filteredRecords = await collection.CountDocumentsAsync(_filter);

			var data = await (await collection.FindAsync(_filter, _findOptions)).ToListAsync();

			return (data, filteredRecords, totalRecords);
		}

		public DatatablesPagingDefinition<T> SortDescendingById()
		{
			_findOptions.Sort = _findOptions.Sort == null ? Builders<T>.Sort.Descending("_id") : _findOptions.Sort.Descending("_id");

			return this;
		}

		private string GetColumnName(string dataName)
		{
			if (char.IsLower(dataName[0]))
				dataName = char.ToUpper(dataName[0]) + dataName.Substring(1);

			return dataName;
		}

		private void AddToFilter(DatatableFiledInfo info, string value)
		{
			if (info == null || string.IsNullOrEmpty(value))
				return;

			if (info.Type == BsonType.String)
			{
				if (_filter == Builders<T>.Filter.Empty)
					_filter = Builders<T>.Filter.Regex(info.Name, new BsonRegularExpression("/" + value + "/i"));
				else
					_filter &= Builders<T>.Filter.Regex(info.Name, new BsonRegularExpression("/" + value + "/i"));
			}
			else if (info.Type == BsonType.ObjectId)
			{
				if (ObjectId.TryParse(value, out var objId))
				{
					if (_filter == Builders<T>.Filter.Empty)
						_filter = Builders<T>.Filter.Eq(info.Name, objId);
					else
						_filter &= Builders<T>.Filter.Eq(info.Name, objId);
				}
			}
		}

		private DatatableFiledInfo GetFieldInfo(Expression<Func<T, object>> field)
		{
			var result = new DatatableFiledInfo() { Type = BsonType.String };
			var member = ((MemberExpression)field.Body).Member;

			result.Name = member.Name;
			if (member.MemberType == MemberTypes.Property)
				result.IsEnumerable = (((PropertyInfo)member).PropertyType.GetInterface(nameof(IEnumerable)) != null);
			else if (member.MemberType == MemberTypes.Field)
				result.IsEnumerable = (((FieldInfo)member).FieldType.GetInterface(nameof(IEnumerable)) != null);

			var attributes = member.CustomAttributes;
			foreach (var attribute in attributes)
			{
				if (attribute.AttributeType == typeof(BsonRepresentationAttribute))
				{
					result.Type = (BsonType)attribute.ConstructorArguments.FirstOrDefault().Value;
					break;
				}
			}

			return result;
		}
	}

	public class Datatables
	{
		public static DatatablesPagingDefinition<T> StartPaging<T>(DataTableProcessParametrs parametrs)
		{
			return new DatatablesPagingDefinition<T>(parametrs);
		}
	}
}
