using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Security;
using System.Configuration.Provider;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Specialized;
using System.Configuration;
using System.Text;
using System.Security.Cryptography;
using CodeSmith.Core.Security;

namespace CodeSmith.Core
{
    public sealed class CustomMembershipProvider : MembershipProvider
    {
        #region Initialization / Configuration

        private string _applicationName;
        private string _connectionString;
        private NameValueCollection _configuration;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name)) name = "CustomMembershipProvider";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Custom Membership Provider");
            }

            base.Initialize(name, config);

            _configuration = config;
            ConnectionStringSettings ConnectionStringSettings = ConfigurationManager.ConnectionStrings[config["connectionStringName"]];
            if (ConnectionStringSettings == null || ConnectionStringSettings.ConnectionString.Trim() == "")
            {
                throw new ProviderException("Connection string cannot be blank.");
            }
            _connectionString = ConnectionStringSettings.ConnectionString;
            _applicationName = GetConfig("applicationName", "");
        }

        public override string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        public override bool EnablePasswordReset
        {
            get { return true; }
        }

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        public override int MaxInvalidPasswordAttempts
        {
            get { return 0; }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { return Convert.ToInt32(this.GetConfig("minRequiredNonAlphanumericCharacters", "0")); }
        }

        public override int MinRequiredPasswordLength
        {
            get { return Convert.ToInt32(this.GetConfig("minRequiredPasswordLength", DefaultMinRequiredPasswordLength.ToString())); }
        }

        public override int PasswordAttemptWindow
        {
            get { return 0; }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get { return false; }
        }

        public override bool RequiresUniqueEmail
        {
            get { return true; }
        }

        public override string PasswordStrengthRegularExpression
        {
            get { return this.GetConfig("passwordStrengthRegularExpression", "^(?=.{8,})(?=.*[A-Z])(?=.*[a-z])(?=.*[0-9])(?=.*\\W).*$"); }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get { return MembershipPasswordFormat.Hashed; }
        }

        #endregion

        #region Provider Methods

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            if (String.IsNullOrEmpty(username) || username.Length > UserNameSize)
            {
                status = MembershipCreateStatus.InvalidUserName;
                return null;
            }
            username = username.ToLower();
            if (this.CheckUserExists(username))
            {
                status = MembershipCreateStatus.DuplicateUserName;
                return null;
            }

            ValidatePasswordEventArgs args = new ValidatePasswordEventArgs(username, password, true);
            OnValidatingPassword(args);
            if (args.Cancel)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            if (!IsEmail(username))
            {
                status = MembershipCreateStatus.InvalidUserName;
                return null;
            }

            if (password.Length < MinRequiredPasswordLength)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            int count = 0;
            for (int i = 0; i < password.Length; i++)
            {
                if (!char.IsLetterOrDigit(password, i))
                {
                    count++;
                }
            }

            if (count < MinRequiredNonAlphanumericCharacters)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            if (PasswordStrengthRegularExpression.Length > 0)
            {
                if (!Regex.IsMatch(password, PasswordStrengthRegularExpression))
                {
                    status = MembershipCreateStatus.InvalidPassword;
                    return null;
                }
            }

            string passwordSalt = Membership.GeneratePassword(5, 1);
            string passwordHash = ComputeSHA512(password + passwordSalt);

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_CreateUser), cn);

                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                cmd.Parameters.Add("@PasswordHash", SqlDbType.Char, 86).Value = passwordHash;
                cmd.Parameters.Add("@PasswordSalt", SqlDbType.Char, 5).Value = passwordSalt;
                cmd.Parameters.Add("@IsApproved", SqlDbType.Bit).Value = isApproved;

                int rowCount = cmd.ExecuteNonQuery();
                if (rowCount == 0)
                {
                    status = MembershipCreateStatus.UserRejected;
                }
                else
                {
                    status = MembershipCreateStatus.Success;
                }
            }

            if (status == MembershipCreateStatus.Success) return this.GetUser(username, false);

            return null;
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            if (String.IsNullOrEmpty(username) || username.Length > UserNameSize) return null;
            username = username.ToLower();

            MembershipUser u = null;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetUser), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.CloseConnection))
                {
                    if (reader.Read()) u = this.GetUserFromReader(reader);
                }
            }

            if (userIsOnline) this.UpdateLastActivityDate(u.UserName);

            return u;
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            if (providerUserKey == null) return null;
            string emailAddress = providerUserKey.ToString();
            
            MembershipUser u = null;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand("", cn);
                cmd.Parameters.Add("@EmailAddress", SqlDbType.NVarChar).Value = emailAddress;
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.CloseConnection))
                {
                    if (reader.Read()) u = this.GetUserFromReader(reader);
                }
            }

            if (userIsOnline) this.UpdateLastActivityDate(u.UserName);

            return u;
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            throw new ProviderException("Password questions are not implemented in this provider.");
        }

        public override string GetPassword(string username, string answer)
        {
            throw new ProviderException("Password retrieval is not possible for hashed passwords.");
        }

        public override string GetUserNameByEmail(string email)
        {
            if (String.IsNullOrEmpty(email) || email.Length > UserNameSize) return String.Empty;

            return email;
        }

        public override bool ValidateUser(string username, string password)
        {
            if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password) || username.Length > UserNameSize) return false;
            username = username.ToLower();

            string passwordHash = String.Empty;
            string passwordSalt = String.Empty;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_ValidateUser), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.CloseConnection))
                {
                    if (reader.Read())
                    {
                        passwordHash = Convert.ToString(reader["PasswordHash"]);
                        passwordSalt = Convert.ToString(reader["PasswordSalt"]);
                    }
                }
            }

            if (String.IsNullOrEmpty(passwordHash) || String.IsNullOrEmpty(passwordSalt)) { return false; }

            if (ComputeSHA512(password + passwordSalt).Equals(passwordHash, StringComparison.OrdinalIgnoreCase))
            {
                this.UpdateLastLoginDate(username);
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!ValidateUser(username, oldPassword)) return false;
            username = username.ToLower();

            if (newPassword.Length < MinRequiredPasswordLength)
            {
                throw new ArgumentException(String.Format("The length of parameter 'newPassword' needs to be greater or equal to '{0}'.", MinRequiredPasswordLength.ToString(CultureInfo.InvariantCulture)));
            }

            int count = 0;

            for (int i = 0; i < newPassword.Length; i++)
            {
                if (!char.IsLetterOrDigit(newPassword, i))
                {
                    count++;
                }
            }

            if (count < MinRequiredNonAlphanumericCharacters)
            {
                throw new ArgumentException(String.Format("Non alpha numeric characters in 'newPassword' needs to be greater than or equal to '{0}'.", MinRequiredNonAlphanumericCharacters.ToString(CultureInfo.InvariantCulture)));
            }

            if (PasswordStrengthRegularExpression.Length > 0)
            {
                if (!Regex.IsMatch(newPassword, PasswordStrengthRegularExpression))
                {
                    throw new ArgumentException("The parameter 'newPassword' does not match the regular expression specified in config file.");
                }
            }

            ValidatePasswordEventArgs args = new ValidatePasswordEventArgs(username, newPassword, true);
            OnValidatingPassword(args);
            if (args.Cancel)
            {
                if (args.FailureInformation != null)
                {
                    throw args.FailureInformation;
                }
                else
                {
                    throw new MembershipPasswordException("Change password canceled due to new password validation failure.");
                }
            }

            return this.SetPassword(username, newPassword);
        }

        public override string ResetPassword(string username, string answer)
        {
            if (!this.CheckUserExists(username)) throw new MembershipPasswordException("User not found");
            username = username.ToLower();

            string newPassword = PasswordGenerator.Generate(Math.Max(8, this.MinRequiredPasswordLength), this.MinRequiredNonAlphanumericCharacters);
            ValidatePasswordEventArgs args = new ValidatePasswordEventArgs(username, newPassword, true);
            OnValidatingPassword(args);
            if (args.Cancel)
            {
                if (args.FailureInformation != null)
                {
                    throw args.FailureInformation;
                }
                else
                {
                    throw new MembershipPasswordException("Reset password canceled due to password validation failure.");
                }
            }

            this.SetPassword(username, newPassword);
            return newPassword;
        }

        public override bool UnlockUser(string username)
        {
            if (!this.CheckUserExists(username)) return false;
            username = username.ToLower();

            int rowCount = 0;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_UnlockUser), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                rowCount = cmd.ExecuteNonQuery();
            }

            return rowCount > 0;
        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            if (!this.CheckUserExists(username)) return false;
            username = username.ToLower();

            int rowCount = 0;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_DeleteUser), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                rowCount = cmd.ExecuteNonQuery();
            }

            return rowCount > 0;
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            if (emailToMatch == null) emailToMatch = String.Empty;
            if (pageIndex < 0) throw new ArgumentOutOfRangeException("pageIndex");
            if (pageSize < 1) throw new ArgumentOutOfRangeException("pageSize");
            emailToMatch = emailToMatch.ToLower().Trim();
            MembershipUserCollection users;
            totalRecords = 0;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_FindUsersByEmail), cn);
                cmd.Parameters.Add("@Criteria", SqlDbType.VarChar, UserNameSize).Value = PrepareCriteria(emailToMatch);
                cmd.Parameters.Add("@PageIndex", SqlDbType.Int).Value = pageIndex;
                cmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;
                SqlParameter totalRecordsParameter = new SqlParameter("@TotalRecords", SqlDbType.Int);
                totalRecordsParameter.Direction = ParameterDirection.Output;
                cmd.Parameters.Add(totalRecordsParameter);

                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    users = this.GetUsersFromReader(reader);
                }

                if (totalRecordsParameter.Value != DBNull.Value) totalRecords = Convert.ToInt32(totalRecordsParameter.Value);
            }

            return users;
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            return this.FindUsersByEmail(usernameToMatch, pageIndex, pageSize, out totalRecords);
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            return this.FindUsersByEmail(String.Empty, pageIndex, pageSize, out totalRecords);
        }

        public override int GetNumberOfUsersOnline()
        {
            int onlineCount = 0;

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_GetNumberOfUsersOnline), cn);
                cmd.Parameters.Add("@LastActivityDate", SqlDbType.DateTime).Value = DateTime.Now.AddMinutes(-Membership.UserIsOnlineTimeWindow);
                onlineCount = (int)cmd.ExecuteScalar();
            }

            return onlineCount;
        }

        public override void UpdateUser(MembershipUser user)
        {
            if (user == null) throw new ArgumentNullException("user");
            if (!this.CheckUserExists(user.UserName)) throw new ArgumentException("User not found", "user");

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_UpdateUser), cn);
                cmd.Parameters.Add("@EmailAddress", SqlDbType.VarChar, UserNameSize).Value = user.UserName.ToLower();
                cmd.Parameters.Add("@Comment", SqlDbType.Text).Value = user.Comment;
                cmd.Parameters.Add("@IsApproved", SqlDbType.Bit).Value = user.IsApproved;
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Helper Methods

        public bool CheckUserExists(string username)
        {
            if (String.IsNullOrEmpty(username)) return false;
            if (username.Length > UserNameSize) throw new ArgumentOutOfRangeException("username", "Maximum length of " + UserNameSize + " characters exceeded");

            bool exists = false;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_CheckUserExists), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username.ToLower();
                exists = (int)cmd.ExecuteScalar() == 1;
            }

            return exists;
        }

        private MembershipUserCollection GetUsersFromReader(SqlDataReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            MembershipUserCollection uc = new MembershipUserCollection();
            while (reader.Read())
            {
                string email = Convert.ToString(reader["EmailAddress"]);
                int commentIndex = reader.GetOrdinal("Comment");
                string comment = reader.IsDBNull(commentIndex) ? String.Empty : reader.GetString(commentIndex);
                bool isApproved = Convert.ToBoolean(reader["IsApproved"]);
                DateTime createdDate = Convert.ToDateTime(reader["CreatedDate"]);
                int lastLoginDateIndex = reader.GetOrdinal("LastLoginDate");
                DateTime lastLoginDate = reader.IsDBNull(lastLoginDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastLoginDateIndex);
                int lastActivityDateIndex = reader.GetOrdinal("LastActivityDate");
                DateTime lastActivityDate = reader.IsDBNull(lastActivityDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastActivityDateIndex);
                int lastPasswordChangedDateIndex = reader.GetOrdinal("LastPasswordChangeDate");
                DateTime lastPasswordChangedDate = reader.IsDBNull(lastPasswordChangedDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastPasswordChangedDateIndex);
                uc.Add(new MembershipUser(this.Name, email, email, email, String.Empty, comment, isApproved, false, createdDate, lastLoginDate, lastActivityDate, lastPasswordChangedDate, DateTime.MinValue));
            }

            return uc;
        }

        private MembershipUser GetUserFromReader(SqlDataReader reader)
        {
            string email = Convert.ToString(reader["EmailAddress"]);
            int commentIndex = reader.GetOrdinal("Comment");
            string comment = reader.IsDBNull(commentIndex) ? String.Empty : reader.GetString(commentIndex);
            bool isApproved = Convert.ToBoolean(reader["IsApproved"]);
            DateTime createdDate = Convert.ToDateTime(reader["CreatedDate"]);
            int lastLoginDateIndex = reader.GetOrdinal("LastLoginDate");
            DateTime lastLoginDate = reader.IsDBNull(lastLoginDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastLoginDateIndex);
            int lastActivityDateIndex = reader.GetOrdinal("LastActivityDate");
            DateTime lastActivityDate = reader.IsDBNull(lastActivityDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastActivityDateIndex);
            int lastPasswordChangedDateIndex = reader.GetOrdinal("LastPasswordChangeDate");
            DateTime lastPasswordChangedDate = reader.IsDBNull(lastPasswordChangedDateIndex) ? DateTime.MinValue : reader.GetDateTime(lastPasswordChangedDateIndex);
            return new MembershipUser(this.Name, email, email, email, String.Empty, comment, isApproved, false, createdDate, lastLoginDate, lastActivityDate, lastPasswordChangedDate, DateTime.MinValue);
        }

        private bool SetPassword(string username, string password)
        {
            string passwordSalt = Membership.GeneratePassword(5, 1);
            string passwordHash = ComputeSHA512(password + passwordSalt);

            int rowCount = 0;
            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_SetPassword), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username;
                cmd.Parameters.Add("@PasswordHash", SqlDbType.Char, 86).Value = passwordHash;
                cmd.Parameters.Add("@PasswordSalt", SqlDbType.Char, 5).Value = passwordSalt;
                rowCount = cmd.ExecuteNonQuery();
            }

            return rowCount > 0;
        }

        private void UpdateLastActivityDate(string username)
        {
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            if (username.Length > UserNameSize) throw new ArgumentOutOfRangeException("username", "Maximum length of " + UserNameSize + " characters exceeded");

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_UpdateLastActivityDate), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username.ToLower();
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateLastLoginDate(string username)
        {
            if (String.IsNullOrEmpty(username)) throw new ArgumentNullException("username");
            if (username.Length > UserNameSize) throw new ArgumentOutOfRangeException("username", "Maximum length of " + UserNameSize + " characters exceeded");

            using (SqlConnection cn = this.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(this.ExpandCommand(SQL_UpdateLastLoginDate), cn);
                cmd.Parameters.Add("@UserName", SqlDbType.VarChar, UserNameSize).Value = username.ToLower();
                cmd.ExecuteNonQuery();
            }
        }

        private static bool IsEmail(string email)
        {
            if (String.IsNullOrEmpty(email) || email.Length > UserNameSize) return false;
            return Regex.IsMatch(email, @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$");
        }

        private SqlConnection OpenConnection()
        {
            SqlConnection cn = new SqlConnection(_connectionString);
            cn.Open();
            return cn;
        }

        private string ExpandCommand(string sql)
        {
            sql = sql.Replace("$MembershipTable", MembershipTableName);

            return sql;
        }

        private string PrepareCriteria(string criteria)
        {
            criteria = criteria.Replace('*', '%');
            criteria = criteria.Replace('?', '_');

            if (criteria.Length == 0) criteria = "%";

            return criteria;
        }

        private string GetConfig(string name, string defaultValue)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("Name");

            string Value = this._configuration[name];
            if (String.IsNullOrEmpty(Value)) Value = defaultValue;

            return Value;
        }

        private static string ComputeSHA512(string s)
        {
            if (String.IsNullOrEmpty(s)) throw new ArgumentNullException();
            byte[] buffer = Encoding.UTF8.GetBytes(s);
            buffer = SHA512Managed.Create().ComputeHash(buffer);

            return Convert.ToBase64String(buffer).Substring(0, 86);
        }

        #endregion

        #region Settings / Templates

        internal const int DefaultMinRequiredPasswordLength = 8;
        internal const string MembershipTableName = "[Exceptionless].[UserAuthorization]";
        internal const int UserNameSize = 256;

        private const string SQL_CreateUser = @"INSERT INTO $MembershipTable (EmailAddress, PasswordHash, PasswordSalt, Comment, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate) VALUES (@UserName, @PasswordHash, @PasswordSalt, NULL, @IsApproved, GETDATE(), NULL, GETDATE(), NULL)";
        private const string SQL_GetUser = @"SELECT EmailAddress, Comment, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate FROM $MembershipTable WHERE EmailAddress = @UserName";
        private const string SQL_GetUserByKey = @"SELECT EmailAddress, Comment, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate FROM $MembershipTable WHERE EmailAddress = @EmailAddress";
        private const string SQL_ValidateUser = @"SELECT PasswordHash, PasswordSalt FROM $MembershipTable WHERE EmailAddress = @UserName AND IsApproved = 1";
        private const string SQL_UnlockUser = @"UPDATE $MembershipTable SET IsApproved = 1 WHERE EmailAddress = @UserName";
        private const string SQL_DeleteUser = @"DELETE FROM $MembershipTable WHERE EmailAddress = @UserName";
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
            	
	            SELECT @TotalRecords = COUNT(EmailAddress) FROM $MembershipTable WHERE EmailAddress LIKE @Criteria;
                
                WITH UserPaged AS
                (
                    SELECT EmailAddress, Comment, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate,
                       ROW_NUMBER() OVER (ORDER BY EmailAddress) AS RowNumber
                    FROM $MembershipTable
		            WHERE EmailAddress LIKE @Criteria
                )
		            SELECT EmailAddress, Comment, IsApproved, CreatedDate, LastLoginDate, LastActivityDate, LastPasswordChangeDate
		            FROM UserPaged
		            WHERE RowNumber >= @RowStart AND RowNumber <= @RowEnd
            END";
        private const string SQL_GetNumberOfUsersOnline = @"SELECT COUNT(*) FROM $MembershipTable WHERE LastActivityDate > @LastActivityDate";
        private const string SQL_UpdateUser = @"UPDATE $MembershipTable SET Comment = @Comment, IsApproved = @IsApproved WHERE EmailAddress = @EmailAddress";
        private const string SQL_CheckUserExists = @"SELECT COUNT(*) FROM $MembershipTable WHERE EmailAddress = @UserName";
        private const string SQL_SetPassword = @"UPDATE $MembershipTable SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, LastActivityDate = GETDATE() WHERE EmailAddress = @UserName";
        private const string SQL_UpdateLastActivityDate = @"UPDATE $MembershipTable SET LastActivityDate = GETDATE() WHERE EmailAddress = @UserName";
        private const string SQL_UpdateLastLoginDate = @"UPDATE $MembershipTable SET LastLoginDate = GETDATE() WHERE EmailAddress = @UserName";

        #endregion
    }
}