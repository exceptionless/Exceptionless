using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.Profile;
using System.Configuration;
using System.Configuration.Provider;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace CodeSmith.Core
{
    public sealed class CustomProfileProvider : ProfileProvider
    {
        #region Initialization / Configuration

        private string _applicationName;
        private string _connectionString;
        private string _tableName;
        private string _lastUpdateColumnName;

        private NameValueCollection _configuration;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name)) name = "CustomProfileProvider";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Custom Profile Provider");
            }

            base.Initialize(name, config);

            _configuration = config;
            _applicationName = GetConfig("applicationName", "");

            ConnectionStringSettings ConnectionStringSettings = ConfigurationManager.ConnectionStrings[config["connectionStringName"]];
            if (ConnectionStringSettings == null || ConnectionStringSettings.ConnectionString.Trim() == "") throw new ProviderException("Connection string cannot be blank.");
            _connectionString = ConnectionStringSettings.ConnectionString;

            _tableName = GetConfig("tableName", "Profiles");
            if (!IsValidDbObjectName(_tableName)) throw new ProviderException("Table name contains illegal characters.");

            _lastUpdateColumnName = GetConfig("lastUpdateColumnName", "LastUpdate");
            if (!IsValidDbObjectName(_lastUpdateColumnName)) throw new ProviderException("Last update column name contains illegal characters.");
        }

        public override string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        public string TableName
        {
            get { return _tableName; }
        }

        public string LastUpdateColumnName
        {
            get { return _lastUpdateColumnName; }
        }

        #endregion

        #region Provider Methods

        public override int DeleteProfiles(string[] usernames)
        {
            if (usernames == null) throw new ArgumentNullException();
            if (usernames.Length == 0) return 0;

            int count = 0;
            using (SqlConnection cn = OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_DeleteProfiles), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize);
                foreach (string username in usernames)
                {
                    cmd.Parameters["@UserName"].Value = username;
                    count += cmd.ExecuteNonQuery();
                }
            }

            return count;
        }

        public override int DeleteProfiles(ProfileInfoCollection profiles)
        {
            if (profiles == null) throw new ArgumentNullException();
            if (profiles.Count == 0) return 0;

            int count = 0;
            using (SqlConnection cn = OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_DeleteProfiles), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize);
                foreach (ProfileInfo pi in profiles)
                {
                    cmd.Parameters["@UserName"].Value = pi.UserName;
                    count += cmd.ExecuteNonQuery();
                }
            }

            return count;
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            SettingsPropertyValueCollection svc = new SettingsPropertyValueCollection();

            if (collection == null || collection.Count < 1 || context == null) return svc;
            string username = (string)context["UserName"];
            if (String.IsNullOrEmpty(username)) return svc;

            DataTable dt = new DataTable();
            using (SqlConnection cn = OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetPropertyValues), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = username;
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);
                da.Dispose();
            }

            foreach (SettingsProperty property in collection)
            {
                SettingsPropertyValue value = new SettingsPropertyValue(property);
                if (dt.Rows.Count == 0)
                {
                    if (!(value.Property.DefaultValue == null
                        || (value.Property.DefaultValue is string && String.IsNullOrEmpty((string)value.Property.DefaultValue))))
                    {
                        value.PropertyValue = Convert.ChangeType(value.Property.DefaultValue, value.Property.PropertyType);
                    }

                    value.IsDirty = false;
                    value.Deserialized = true;
                }
                else
                {
                    string columnName = GetPropertyMapInfo(property).ColumnName; if (dt.Columns.IndexOf(columnName) == -1) throw new ProviderException(String.Format("Column '{0}' required for property '{1}' was not found in table '{2}'.", columnName, property.Name, this.TableName));
                    object columnValue = dt.Rows[0][columnName];

                    if (!(columnValue is DBNull || columnValue == null))
                    {
                        value.PropertyValue = columnValue;
                        value.IsDirty = false;
                        value.Deserialized = true;
                    }
                }
                svc.Add(value);
            }

            return svc;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            if (!(bool)context["IsAuthenticated"]) throw new NotSupportedException("This provider does not support anonymous profiles");
            string username = (string)context["UserName"];
            if (String.IsNullOrEmpty(username) || collection.Count == 0 || !this.HasDirtyProperties(collection)) return;

            StringBuilder updateValues = new StringBuilder();
            StringBuilder insertColumns = new StringBuilder();
            StringBuilder insertValues = new StringBuilder();
            SqlCommand cmd = new SqlCommand();

            int i = 0;
            foreach (SettingsPropertyValue value in collection)
            {
                PropertyMapInfo pmi = GetPropertyMapInfo(value.Property);

                SqlParameter p = new SqlParameter("@Param" + i, pmi.Type);
                if (pmi.Length != 0) p.Size = pmi.Length;
                if (value.Deserialized && value.PropertyValue == null)
                {
                    p.Value = DBNull.Value;
                }
                else
                {
                    p.Value = value.PropertyValue;
                }

                cmd.Parameters.Add(p);

                insertColumns.Append(", " + pmi.ColumnName);
                insertValues.Append(", @Param" + i);

                if (value.IsDirty) updateValues.AppendFormat(", {0} = @Param{1}", pmi.ColumnName, i);

                i++;
            }

            using (SqlConnection cn = OpenConnection())
            {
                string sql = this.ExpandCommand(SQL_SetPropertyValues);
                sql = sql.Replace("$UpdateValues", updateValues.ToString());
                sql = sql.Replace("$InsertColumns", insertColumns.ToString());
                sql = sql.Replace("$InsertValues", insertValues.ToString());
                cmd.Connection = cn;
                cmd.CommandText = sql;
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = username;
                cmd.ExecuteNonQuery();
            }
        }

        public override ProfileInfoCollection FindProfilesByUserName(ProfileAuthenticationOption authenticationOption, string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            if (pageIndex < 0) throw new ArgumentOutOfRangeException("pageIndex");
            if (pageSize < 1) throw new ArgumentOutOfRangeException("pageSize");
            if (authenticationOption == ProfileAuthenticationOption.Anonymous)
            {
                totalRecords = 0;
                return new ProfileInfoCollection();
            }
            ProfileInfoCollection profiles = new ProfileInfoCollection();
            totalRecords = 0;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_FindProfilesByUserName), cn);
                cmd.Parameters.Add("@Criteria", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = PrepareCriteria(usernameToMatch);
                cmd.Parameters.Add("@PageIndex", SqlDbType.Int).Value = pageIndex;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
                SqlParameter totalRecordsParameter = new SqlParameter("@TotalRecords", SqlDbType.Int);
                totalRecordsParameter.Direction = ParameterDirection.Output;
                cmd.Parameters.Add(totalRecordsParameter);

                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    while (reader.Read())
                    {
                        profiles.Add(new ProfileInfo(Convert.ToString(reader["EmailAddress"]), false, DateTime.Now, Convert.ToDateTime(reader["LastUpdateDate"]), 0));
                    }
                }

                if (totalRecordsParameter.Value != DBNull.Value) totalRecords = Convert.ToInt32(totalRecordsParameter.Value);
            }

            return profiles;
        }

        public override ProfileInfoCollection GetAllProfiles(ProfileAuthenticationOption authenticationOption, int pageIndex, int pageSize, out int totalRecords)
        {
            return FindProfilesByUserName(authenticationOption, String.Empty, pageIndex, pageSize, out totalRecords);
        }

        #region Inactive Profiles - Not Implemented

        public override int DeleteInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate)
        {
            throw new NotImplementedException();
        }

        public override ProfileInfoCollection FindInactiveProfilesByUserName(ProfileAuthenticationOption authenticationOption, string usernameToMatch, DateTime userInactiveSinceDate, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotImplementedException();
        }

        public override ProfileInfoCollection GetAllInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotImplementedException();
        }

        public override int GetNumberOfInactiveProfiles(ProfileAuthenticationOption authenticationOption, DateTime userInactiveSinceDate)
        {
            throw new NotImplementedException();
        }

        #endregion

        #endregion

        #region Helper Methods

        private struct PropertyMapInfo
        {
            public string ColumnName;
            public SqlDbType Type;
            public int Length;
        }

        private PropertyMapInfo GetPropertyMapInfo(SettingsProperty property)
        {
            if (property == null) throw new ArgumentNullException();
            string cpd = Convert.ToString(property.Attributes["CustomProviderData"]);
            if (String.IsNullOrEmpty(cpd)) throw new ProviderException(String.Format("CustomProviderData is missing or empty for property {0}.", property.Name));
            if (!Regex.IsMatch(cpd, CustomProviderDataFormat)) throw new ProviderException(String.Format("Invalid format of CustomProviderData for property {0}.", property.Name));
            string[] parts = cpd.Split(';');

            PropertyMapInfo pmi = new PropertyMapInfo();
            pmi.ColumnName = parts[0];
            try
            {
                pmi.Type = (SqlDbType)Enum.Parse(typeof(SqlDbType), parts[1], true);
            }
            catch
            {
                throw new ProviderException(String.Format("SqlDbType '{0}' specified for property {1} is invalid.", parts[1], property.Name));
            }
            if (parts.Length == 3) pmi.Length = Convert.ToInt32(parts[2]);

            return pmi;
        }

        private bool HasDirtyProperties(SettingsPropertyValueCollection props)
        {
            foreach (SettingsPropertyValue prop in props)
            {
                if (prop.IsDirty) return true;
            }

            return false;
        }

        private string ExpandCommand(string sql)
        {
            sql = sql.Replace("$ProfileTable", this.TableName);
            sql = sql.Replace("$MembershipTable", CustomMembershipProvider.MembershipTableName);
            sql = sql.Replace("$LastUpdateColumn", this.LastUpdateColumnName);

            return sql;
        }

        private string PrepareCriteria(string criteria)
        {
            criteria = criteria.Replace('*', '%');
            criteria = criteria.Replace('?', '_');

            if (criteria.Length == 0) criteria = "%";

            return criteria;
        }

        private SqlConnection OpenConnection()
        {
            SqlConnection cn = new SqlConnection(_connectionString);
            cn.Open();

            return cn;
        }

        private string GetConfig(string name, string defaultValue)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("Name");

            string value = _configuration[name];
            if (String.IsNullOrEmpty(value)) value = defaultValue;

            return value;
        }

        private static bool IsValidDbObjectName(string s)
        {
            if (String.IsNullOrEmpty(s)) return false;

            return Regex.IsMatch(s, DbObjectNameFormat);
        }

        #endregion

        #region Settings / Templates

        private const string DbObjectNameFormat = @"^\[?[a-zA-Z0-9_\.]{1,}\]?$";
        private const string CustomProviderDataFormat = @"^[a-zA-Z0-9_]+;[a-zA-Z0-9_]+(;[0-9]{1,})?$";

        private const string SQL_DeleteProfiles = @"DELETE FROM $ProfileTable FROM $ProfileTable AS p INNER JOIN $MembershipTable AS u ON p.UserId = u.Id WHERE u.EmailAddress = @UserName";
        private const string SQL_GetPropertyValues = @"SELECT p.* FROM $ProfileTable AS p INNER JOIN $MembershipTable AS u ON p.UserId = u.Id WHERE u.EmailAddress = @UserName";
        private const string SQL_FindProfilesByUserName = @"SELECT u.EmailAddress AS UserName, p.$LastUpdateColumn AS LastUpdateDate FROM $ProfileTable AS p INNER JOIN $MembershipTable AS u ON p.UserId = u.Id WHERE e.EmailAddress LIKE @UserName ORDER BY e.EmailAddress";
        private const string SQL_FindUsersByEmail = @"
            DECLARE @RowStart int 
            DECLARE @RowEnd int 
            
            IF @PageIndex >= 0 
            BEGIN
                SET @RowStart = @PageSize * @PageIndex + 1
	            IF @PageSize < 2147483647
		            SET @RowEnd = @RowStart + @PageSize - 1
	            ELSE
		            SET @RowEnd = 2147483647
            	
	            SELECT @TotalRecords = COUNT(p.Id) FROM $ProfileTable AS p INNER JOIN $MembershipTable AS u ON p.UserId = u.Id WHERE EmailAddress LIKE @Criteria;
                
                WITH ProfilePaged AS
                (
                    SELECT u.EmailAddress AS EmailAddress, p.$LastUpdateColumn AS LastUpdateDate,
                       ROW_NUMBER() OVER (ORDER BY u.EmailAddress) AS RowNumber
                    FROM $ProfileTable AS p INNER JOIN $MembershipTable AS u ON p.UserId = u.Id
		            WHERE u.EmailAddress LIKE @Criteria
                )
		            SELECT EmailAddress, LastUpdateDate
		            FROM ProfilePaged
		            WHERE RowNumber >= @RowStart AND RowNumber <= @RowEnd
            END";
        private const string SQL_SetPropertyValues = @"
            DECLARE @UserId int
            DECLARE @ProfileId int
            
            SELECT @UserId = Id FROM [User] WHERE EmailAddress = @UserName
            SELECT @ProfileId = Id FROM UserProfile WHERE UserId = @UserId
            
            IF @ProfileId IS NOT NULL
            BEGIN
                UPDATE $ProfileTable SET $LastUpdateColumn = GETDATE() $UpdateValues WHERE Id = @ProfileId
            END
            ELSE
            BEGIN
                INSERT INTO $ProfileTable (UserId, $LastUpdateColumn $InsertColumns) VALUES (@UserId, GETDATE() $InsertValues)
            END";

        #endregion
    }
}
