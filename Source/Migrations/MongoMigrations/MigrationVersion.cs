using System;

namespace MongoMigrations {
    public struct MigrationVersion : IComparable<MigrationVersion> {
        /// <summary>
        /// 	Return the default, "first" version 0.0.0
        /// </summary>
        /// <returns></returns>
        public static MigrationVersion Default() {
            return default(MigrationVersion);
        }

        public int Major;
        public int Minor;
        public int Revision;

        public MigrationVersion(string version) {
            var versionParts = version.Split('.');
            if (versionParts.Length != 3) {
                throw new ArgumentException("Versions must have format: major.minor.revision, this doesn't match: " + version);
            }
            var majorString = versionParts[0];
            if (!Int32.TryParse(majorString, out Major)) {
                throw new ArgumentException("Invalid major version value: " + majorString);
            }
            var minorString = versionParts[1];
            if (!Int32.TryParse(minorString, out Minor)) {
                throw new ArgumentException("Invalid major version value: " + minorString);
            }
            var revisionString = versionParts[2];
            if (!Int32.TryParse(revisionString, out Revision)) {
                throw new ArgumentException("Invalid major version value: " + revisionString);
            }
        }

        public MigrationVersion(int major, int minor, int revision) {
            Major = major;
            Minor = minor;
            Revision = revision;
        }

        public static bool operator ==(MigrationVersion a, MigrationVersion b) {
            return a.Equals(b);
        }

        public static bool operator !=(MigrationVersion a, MigrationVersion b) {
            return !(a == b);
        }

        public static bool operator >(MigrationVersion a, MigrationVersion b) {
            return a.Major > b.Major
                   || (a.Major == b.Major && a.Minor > b.Minor)
                   || (a.Major == b.Major && a.Minor == b.Minor && a.Revision > b.Revision);
        }

        public static bool operator <(MigrationVersion a, MigrationVersion b) {
            return a != b && !(a > b);
        }

        public static bool operator <=(MigrationVersion a, MigrationVersion b) {
            return a == b || a < b;
        }

        public static bool operator >=(MigrationVersion a, MigrationVersion b) {
            return a == b || a > b;
        }

        public bool Equals(MigrationVersion other) {
            return other.Major == Major && other.Minor == Minor && other.Revision == Revision;
        }

        public int CompareTo(MigrationVersion other) {
            if (Equals(other)) {
                return 0;
            }
            return this > other ? 1 : -1;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (obj.GetType() != typeof(MigrationVersion))
                return false;
            return Equals((MigrationVersion)obj);
        }

        public override int GetHashCode() {
            unchecked {
                var result = Major;
                result = (result * 397) ^ Minor;
                result = (result * 397) ^ Revision;
                return result;
            }
        }

        public override string ToString() {
            return string.Format("{0}.{1}.{2}", Major, Minor, Revision);
        }

        public static implicit operator MigrationVersion(string version) {
            return new MigrationVersion(version);
        }

        public static implicit operator string(MigrationVersion version) {
            return version.ToString();
        }
    }
}