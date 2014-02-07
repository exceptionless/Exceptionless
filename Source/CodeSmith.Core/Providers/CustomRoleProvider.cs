using System;
using System.Web.Security;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Configuration;
using System.Collections.Generic;

namespace CodeSmith.Core
{
    public sealed class CustomRoleProvider : RoleProvider
    {
        #region Initialization / Configuration

        private string _applicationName;
        private string _connectionString;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name)) name = "CustomRoleProvider";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Custom Role Provider");
            }

            base.Initialize(name, config);

            ConnectionStringSettings ConnectionStringSettings = ConfigurationManager.ConnectionStrings[config["connectionStringName"]];
            if (ConnectionStringSettings == null || ConnectionStringSettings.ConnectionString.Trim() == "") throw new ProviderException("Connection string cannot be blank.");
            _connectionString = ConnectionStringSettings.ConnectionString;
            _applicationName = config["applicationName"];
        }

        public override string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        #endregion

        #region Provider Methods

        public override void CreateRole(string rolename)
        {
            if (String.IsNullOrEmpty(rolename)) throw new ArgumentNullException("rolename");
            if (rolename.IndexOf(',') > 0) throw new ArgumentException("Role names cannot contain commas");
            if (rolename.Length > RoleNameSize) throw new ArgumentException("Maximum role name length is " + RoleNameSize + " characters");
            if (this.RoleExists(rolename)) throw new ProviderException("Role name already exists");
            rolename = rolename.ToLower();

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_CreateRole), cn);
                cmd.Parameters.Add("@Rolename", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                cmd.ExecuteNonQuery();
            }
        }

        public override bool DeleteRole(string rolename, bool throwOnPopulatedRole)
        {
            if (String.IsNullOrEmpty(rolename)) throw new ArgumentNullException("rolename");
            if (!this.RoleExists(rolename)) throw new ProviderException("Role does not exist");
            if (throwOnPopulatedRole && this.GetUsersInRole(rolename).Length > 0) throw new ProviderException("Cannot delete a populated role");
            rolename = rolename.ToLower();
            int rowCount = 0;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_DeleteRole), cn);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                rowCount = cmd.ExecuteNonQuery();
            }

            return rowCount > 0;
        }

        public override void AddUsersToRoles(string[] usernames, string[] rolenames)
        {
            foreach (string rolename in rolenames) if (!this.RoleExists(rolename)) throw new ProviderException("Role name not found");
            foreach (string username in usernames)
            {
                if (username.IndexOf(',') > 0) throw new ArgumentException("User names cannot contain commas.");
                foreach (string rolename in rolenames)
                {
                    if (IsUserInRole(username, rolename)) throw new ProviderException("User is already in role.");
                }
            }

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_AddUsersToRoles), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize);

                using (SqlTransaction tran = cn.BeginTransaction())
                {
                    cmd.Transaction = tran;
                    foreach (string username in usernames)
                    {
                        foreach (string rolename in rolenames)
                        {
                            cmd.Parameters["@UserName"].Value = username;
                            cmd.Parameters["@RoleName"].Value = rolename;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tran.Commit();
                }
            }
        }

        public override string[] GetAllRoles()
        {
            string[] roles;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetAllRoles), cn);
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    roles = ReaderToArray(reader);
                }
            }

            return roles;
        }

        public override string[] GetRolesForUser(string username)
        {
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            if (username.IndexOf(',') > -1) throw new ArgumentException("User name cannot contain comma", "username");
            if (username.Length > CustomMembershipProvider.UserNameSize) throw new ArgumentException("User name cannot be longer than " + CustomMembershipProvider.UserNameSize + " characters", "username");
            username = username.ToLower();
            string[] roles;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetRolesForUser), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = username;
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    roles = ReaderToArray(reader);
                }
            }

            return roles;
        }

        public override string[] GetUsersInRole(string rolename)
        {
            if (String.IsNullOrEmpty(rolename)) throw new ArgumentNullException("rolename");
            if (rolename.IndexOf(',') > -1) throw new ArgumentException("Role name cannot contain comma", "rolename");
            if (rolename.Length > RoleNameSize) throw new ArgumentException("Role name cannot be longer than " + RoleNameSize + " characters", "rolename");
            rolename = rolename.ToLower();
            string[] users;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetUsersInRole), cn);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    users = ReaderToArray(reader);
                }
            }

            return users;
        }

        public override bool IsUserInRole(string username, string rolename)
        {
            if (String.IsNullOrEmpty(rolename)) throw new ArgumentNullException("rolename");
            if (rolename.IndexOf(',') > -1) throw new ArgumentException("Role name cannot contain comma", "rolename");
            if (rolename.Length > RoleNameSize) throw new ArgumentException("Role name cannot be longer than " + RoleNameSize + " characters", "rolename");
            rolename = rolename.ToLower();
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            if (username.IndexOf(',') > -1) throw new ArgumentException("User name cannot contain comma", "username");
            if (username.Length > CustomMembershipProvider.UserNameSize) throw new ArgumentException("User name cannot be longer than " + CustomMembershipProvider.UserNameSize + " characters", "username");
            username = username.ToLower();

            int rowCount = 0;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_IsUserInRole), cn);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = username;
                rowCount = (int)cmd.ExecuteScalar();
            }

            return rowCount > 0;
        }

        public override void RemoveUsersFromRoles(string[] usernames, string[] rolenames)
        {
            foreach (string rolename in rolenames) if (!RoleExists(rolename)) throw new ProviderException("Role name not found.");
            foreach (string username in usernames)
            {
                foreach (string rolename in rolenames)
                {
                    if (!IsUserInRole(username, rolename)) throw new ProviderException("User is not in role.");
                }
            }

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_RemoveUsersFromRoles), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize);

                using (SqlTransaction tran = cn.BeginTransaction())
                {
                    cmd.Transaction = tran;

                    foreach (string username in usernames)
                    {
                        foreach (string rolename in rolenames)
                        {
                            cmd.Parameters["@UserName"].Value = username;
                            cmd.Parameters["@RoleName"].Value = rolename;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                }
            }
        }

        public override bool RoleExists(string rolename)
        {
            if (String.IsNullOrEmpty(rolename)) return false;
            rolename = rolename.ToLower();
            int rowCount = 0;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_RoleExists), cn);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                rowCount = (int)cmd.ExecuteScalar();
            }

            return rowCount > 0;
        }

        public override string[] FindUsersInRole(string rolename, string usernameToMatch)
        {
            if (String.IsNullOrEmpty(rolename)) throw new ArgumentNullException("rolename");
            if (rolename.IndexOf(',') > -1) throw new ArgumentException("Role name cannot contain comma", "rolename");
            if (rolename.Length > RoleNameSize) throw new ArgumentException("Role name cannot be longer than " + RoleNameSize + " characters", "rolename");
            rolename = rolename.ToLower();
            if (String.IsNullOrEmpty(usernameToMatch)) throw new ArgumentNullException("usernameToMatch");
            if (usernameToMatch.Length > CustomMembershipProvider.UserNameSize) throw new ArgumentException("User name cannot be longer than " + CustomMembershipProvider.UserNameSize + " characters", "usernameToMatch");
            usernameToMatch = usernameToMatch.ToLower();
            string[] users;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_FindUsersInRole), cn);
                cmd.Parameters.Add("@RoleName", SqlDbType.VarChar, RoleNameSize).Value = rolename;
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, CustomMembershipProvider.UserNameSize).Value = PrepareCriteria(usernameToMatch);
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    users = ReaderToArray(reader);
                }
            }

            return users;
        }

        #endregion

        #region Helper Methods

        private SqlConnection OpenConnection()
        {
            SqlConnection cn = new SqlConnection(this._connectionString);
            cn.Open();

            return cn;
        }

        private string[] ReaderToArray(IDataReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            List<string> data = new List<string>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0)) data.Add(Convert.ToString(reader[0]));
            }

            return data.ToArray();
        }

        private string ExpandCommand(string sql)
        {
            sql = sql.Replace("$MembershipTable", CustomMembershipProvider.MembershipTableName);
            sql = sql.Replace("$RoleTable", RoleTableName);
            sql = sql.Replace("$RoleMembershipTable", RoleMembershipTableName);

            return sql;
        }

        private string PrepareCriteria(string criteria)
        {
            criteria = criteria.Replace('*', '%');
            criteria = criteria.Replace('?', '_');

            if (criteria.Length == 0) criteria = "%";

            return criteria;
        }

        #endregion

        #region Settings / Templates

        internal const string RoleTableName = "[Exceptionless].[Role]";
        internal const string RoleMembershipTableName = "[Exceptionless].[UserRole]";
        internal const int RoleNameSize = 256;

        private const string SQL_CreateRole = @"INSERT INTO $RoleTable (RoleName) VALUES (@RoleName)";
        private const string SQL_DeleteRole = @"DELETE FROM $RoleTable WHERE RoleName = @RoleName";
        private const string SQL_AddUsersToRoles = @"INSERT INTO $RoleMembershipTable (UserId, RoleId) SELECT (SELECT Id FROM $MembershipTable WHERE EmailAddress = @UserName), (SELECT Id FROM $RoleTable WHERE RoleName = @RoleName)";
        private const string SQL_GetAllRoles = @"SELECT RoleName FROM $RoleTable";
        private const string SQL_GetRolesForUser = @"SELECT RoleName FROM $RoleMembershipTable AS ur INNER JOIN $RoleTable AS r ON ur.RoleId = r.Id INNER JOIN $MembershipTable AS u ON ur.UserId = u.Id WHERE EmailAddress = @UserName";
        private const string SQL_GetUsersInRole = @"SELECT EmailAddress FROM $RoleMembershipTable AS ur INNER JOIN $RoleTable AS r ON ur.RoleId = r.Id INNER JOIN $MembershipTable AS u ON ur.UserId = u.Id WHERE RoleName = @RoleName";
        private const string SQL_IsUserInRole = @"SELECT COUNT(*) FROM $RoleMembershipTable AS ur INNER JOIN $RoleTable AS r ON ur.RoleId = r.Id INNER JOIN $MembershipTable AS u ON ur.UserId = u.Id WHERE RoleName = @RoleName AND EmailAddress = @UserName";
        private const string SQL_RemoveUsersFromRoles = @"DELETE FROM $RoleMembershipTable FROM $RoleMembershipTable AS ur INNER JOIN $RoleTable AS r ON ur.RoleId = r.Id INNER JOIN $MembershipTable AS u ON ur.UserId = u.Id WHERE EmailAddress = @UserName AND RoleName = @RoleName";
        private const string SQL_RoleExists = @"SELECT COUNT(*) FROM $RoleTable WHERE RoleName = @RoleName";
        private const string SQL_FindUsersInRole = @"SELECT EmailAddress FROM $RoleMembershipTable AS ur INNER JOIN $RoleTable AS r ON ur.RoleId = r.Id INNER JOIN $MembershipTable AS u ON ur.UserId = u.Id WHERE RoleName = @RoleName AND EmailAddress LIKE @UserName";

        #endregion
    }
}