//VERSION 1.4.2.0
// Install-Package CompareNETObjects
//http://comparenetobjects.codeplex.com/

//Uncomment to see breadcrumb messages in the debug window
//#define BREADCRUMB

//Uncomment to use settings from the app.config
//#define USE_SETTINGS

#region Includes
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
#if !SILVERLIGHT
using System.Data;
#endif

#if USE_SETTINGS
using KellermanSoftware.CompareNETObjects.Properties;
#endif

#endregion

//This software is provided free of charge from Kellerman Software.
//It may be used in any project, including commercial for sale projects.
//
//Check out our other great software at www.kellermansoftware.com:
// *  Free Quick Reference Pack for Developers
// *  Free Sharp Zip Wrapper
// *  NUnit Test Generator
// * .NET Caching Library
// * .NET Email Validation Library
// * .NET FTP Library
// * .NET Encryption Library
// * .NET Logging Library
// * Themed Winform Wizard
// * Unused Stored Procedures
// * AccessDiff
// * .NET SFTP Library
// * Ninja Database Pro

#region License
//Microsoft Public License (Ms-PL)

//This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.

//1. Definitions

//The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law.

//A "contribution" is the original software, or any additions or changes to the software.

//A "contributor" is any person that distributes its contribution under this license.

//"Licensed patents" are a contributor's patent claims that read directly on its contribution.

//2. Grant of Rights

//(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.

//(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

//3. Conditions and Limitations

//(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.

//(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.

//(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.

//(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.

//(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
#endregion

namespace KellermanSoftware.CompareNetObjects
{
    /// <summary>
    /// Class that allows comparison of two objects of the same type to each other.  Supports classes, lists, arrays, dictionaries, child comparison and more.
    /// </summary>
    /// <example>
    /// 
    /// CompareObjects compareObjects = new CompareObjects();
    /// 
    /// Person person1 = new Person();
    /// person1.DateCreated = DateTime.Now;
    /// person1.Name = "Greg";
    ///
    /// Person person2 = new Person();
    /// person2.Name = "John";
    /// person2.DateCreated = person1.DateCreated;
    ///
    /// if (!compareObjects.Compare(person1, person2))
    ///    Console.WriteLine(compareObjects.DifferencesString);
    /// 
    /// </example>
    public class CompareObjects
    {
        #region Class Variables

        /// <summary>
        /// Keep track of parent objects in the object hierarchy
        /// </summary>
        private readonly List<object> _parents = new List<object>();

        /// <summary>
        /// Reflection Cache for property info
        /// </summary>
        private readonly Dictionary<Type, PropertyInfo[]> _propertyCache = new Dictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// Reflection Cache for field info
        /// </summary>
        private readonly Dictionary<Type, FieldInfo[]> _fieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Reflection Cache for methods
        /// </summary>
        private static readonly Dictionary<Type, MethodInfo[]> _methodList = new Dictionary<Type, MethodInfo[]>();
        #endregion

        #region Properties

        /// <summary>
        /// Ignore classes, properties, or fields by name during the comparison.
        /// Case sensitive.
        /// </summary>
        /// <example>ElementsToIgnore.Add("CreditCardNumber")</example>
        public List<string> ElementsToIgnore { get; set; }

        /// <summary>
        /// Ignore classes, properties, or fields by name during the comparison.
        /// Case sensitive.
        /// </summary>
        /// <example>ElementsToIgnore.Add("CreditCardNumber")</example>
        public List<string> ElementsToInclude { get; set; }

//Security restriction in Silverlight prevents getting private properties and fields
#if !SILVERLIGHT
        /// <summary>
        /// If true, private properties and fields will be compared. The default is false.
        /// </summary>
        public bool ComparePrivateProperties { get; set; }

        /// <summary>
        /// If true, private fields will be compared. The default is false.
        /// </summary>
        public bool ComparePrivateFields { get; set; }
#endif

        /// <summary>
        /// If true, static properties will be compared.  The default is true.
        /// </summary>
        public bool CompareStaticProperties { get; set; }

        /// <summary>
        /// If true, static fields will be compared.  The default is true.
        /// </summary>
        public bool CompareStaticFields { get; set; }

        /// <summary>
        /// If true, child objects will be compared. The default is true. 
        /// If false, and a list or array is compared list items will be compared but not their children.
        /// </summary>
        public bool CompareChildren { get; set; }

        /// <summary>
        /// If true, compare read only properties (only the getter is implemented).
        /// The default is true.
        /// </summary>
        public bool CompareReadOnly { get; set; }

        /// <summary>
        /// If true, compare fields of a class (see also CompareProperties).
        /// The default is true.
        /// </summary>
        public bool CompareFields { get; set; }

        /// <summary>
        /// If true, compare properties of a class (see also CompareFields).
        /// The default is true.
        /// </summary>
        public bool CompareProperties { get; set; }

        /// <summary>
        /// The maximum number of differences to detect
        /// </summary>
        /// <remarks>
        /// Default is 1 for performance reasons.
        /// </remarks>
        public int MaxDifferences { get; set; }

        /// <summary>
        /// The differences found during the compare
        /// </summary>
        public List<Difference> Differences { get; set; }

        /// <summary>
        /// The differences found in a string suitable for a textbox
        /// </summary>
        public string DifferencesString
        {
            get
            {
                StringBuilder sb = new StringBuilder(4096);

                sb.Append("\r\nBegin Differences:\r\n");

                foreach (var item in Differences)
                {
                    sb.AppendFormat("{0}\r\n", item);
                }

                sb.AppendFormat("End Differences (Maximum of {0} differences shown).", MaxDifferences);

                return sb.ToString();
            }
        }

        /// <summary>
        /// Reflection properties and fields are cached. By default this cache is cleared after each compare.  Set to false to keep the cache for multiple compares.
        /// </summary>
        /// <seealso cref="Caching"/>
        /// <seealso cref="ClearCache"/>
        public bool AutoClearCache { get; set; }

        /// <summary>
        /// By default properties and fields for types are cached for each compare.  By default this cache is cleared after each compare.
        /// </summary>
        /// <seealso cref="AutoClearCache"/>
        /// <seealso cref="ClearCache"/>
        public bool Caching { get; set; }

        /// <summary>
        /// A list of attributes to ignore a class, property or field
        /// </summary>
        /// <example>AttributesToIgnore.Add(typeof(XmlIgnoreAttribute));</example>
        public List<Type> AttributesToIgnore { get; set; }

        /// <summary>
        /// If true, objects will be compared ignore their type differences
        /// </summary>
        public bool IgnoreObjectTypes { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Set up defaults for the comparison
        /// </summary>
        public CompareObjects()
        {
            Differences = new List<Difference>();
            AttributesToIgnore = new List<Type>();

#if !USE_SETTINGS
            ElementsToIgnore = new List<string>();
            ElementsToInclude = new List<string>();
            CompareStaticFields = true;
            CompareStaticProperties = true;
#if !SILVERLIGHT
            ComparePrivateProperties = false;
            ComparePrivateFields = false;
#endif
            CompareChildren = true;
            CompareReadOnly = true;
            CompareFields = true;
            CompareProperties = true;
            Caching = true;
            AutoClearCache = true;
            IgnoreObjectTypes = false;
            MaxDifferences = 1;
#else
            ElementsToIgnore = Settings.Default.ElementsToIgnore == null
                ? new List<string>()
                : new List<string>((IEnumerable<string>)Settings.Default.ElementsToIgnore);

            if (Settings.Default.ElementsToIgnore != null)
            {
                foreach (var attribute in Settings.Default.ElementsToIgnore)
                {
                    AttributesToIgnore.Add(Type.GetType(attribute));
                }
            }

            CompareStaticFields = Settings.Default.CompareStaticFields;
            CompareStaticProperties = Settings.Default.CompareStaticProperties;

            #if !SILVERLIGHT
            ComparePrivateProperties = Settings.Default.ComparePrivateProperties;
            ComparePrivateFields = Settings.Default.ComparePrivateFields;
            #endif

            CompareChildren = Settings.Default.CompareChildren;
            CompareReadOnly = Settings.Default.CompareReadOnly;
            CompareFields = Settings.Default.CompareFields;
            CompareProperties = Settings.Default.CompareProperties;
            Caching = Settings.Default.Caching;
            AutoClearCache = Settings.Default.AutoClearCache;
            MaxDifferences = 1;
            int maxDifferences;
            if (Int32.TryParse(Settings.Default.MaxDifferences, out maxDifferences))
                MaxDifferences = maxDifferences;
#endif


        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Compare two objects of the same type to each other.
        /// </summary>
        /// <remarks>
        /// Check the Differences or DifferencesString Properties for the differences.
        /// Default MaxDifferences is 1 for performance
        /// </remarks>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <returns>True if they are equal</returns>
        public bool Compare(object object1, object object2)
        {
            string defaultBreadCrumb = String.Empty;

            Differences.Clear();
            Compare(object1, object2, defaultBreadCrumb);

            if (AutoClearCache)
                ClearCache();

            return Differences.Count == 0;
        }

        /// <summary>
        /// Reflection properties and fields are cached. By default this cache is cleared automatically after each compare.
        /// </summary>
        /// <seealso cref="AutoClearCache"/>
        /// <seealso cref="Caching"/>
        public void ClearCache()
        {
            _propertyCache.Clear();
            _fieldCache.Clear();
            _methodList.Clear();
        }

        #endregion

        #region Comparison Methods

        /// <summary>
        /// Compare two objects
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb">Where we are in the object hierarchy</param>
        private void Compare(object object1, object object2, string breadCrumb)
        {
            //If both null return true
            if (object1 == null && object2 == null)
                return;

            //Check if one of them is null
            if (object1 == null)
            {
                Differences.Add(new Difference(breadCrumb, "(null)", NiceString(object2)));
                return;
            }

            if (object2 == null)
            {
                Differences.Add(new Difference(breadCrumb, NiceString(object1), "(null)"));
                return;
            }

            Type t1 = object1.GetType();
            Type t2 = object2.GetType();

            //Objects must be the same type
            if (t1 != t2 && !IgnoreObjectTypes)
            {
                Differences.Add(new Difference(breadCrumb, t1.FullName, t2.FullName, "GetType()", "Different Types"));
                return;
            }

            if (IsTypeOfType(t1))
            {
                CompareType(object1, object2, breadCrumb);
            }
#if !SILVERLIGHT
            else if (IsDataset(t1))
            {
                CompareDataset(object1, object2, breadCrumb);
            }
            else if (IsDataTable(t1))
            {
                CompareDataTable(object1, object2, breadCrumb);
            }
            else if (IsDataRow(t1))
            {
                CompareDataRow(object1, object2, breadCrumb);
            }
#endif
            else if (IsIList(t1)) //This will do arrays, multi-dimensional arrays and generic lists
            {
                CompareIList(object1, object2, breadCrumb);
            }
            else if (IsHashSet(t1))
            {
                CompareHashSet(object1,object2,breadCrumb);
            }
            else if (IsIDictionary(t1))
            {
                CompareIDictionary(object1, object2, breadCrumb);
            }
            else if (IsEnum(t1))
            {
                CompareEnum(object1, object2, breadCrumb);
            }
            else if (IsPointer(t1))
            {
                ComparePointer(object1, object2, breadCrumb);
            }
            else if (IsUri(t1))
            {
                CompareUri(object1, object2, breadCrumb);
            }
            else if (IsSimpleType(t1))
            {
                CompareSimpleType(object1, object2, breadCrumb);
            }
            else if (IsClass(t1))
            {
                CompareClass(object1, object2, breadCrumb);
            }
            else if (IsTimespan(t1))
            {
                CompareTimespan(object1, object2, breadCrumb);
            }
            else if (IsStruct(t1))
            {
                CompareStruct(object1, object2, breadCrumb);
            }
            else
            {
                throw new NotImplementedException("Cannot compare object of type " + t1.Name);
            }

        }

        private void CompareUri(object object1, object object2, string breadCrumb)
        {
            Uri uri1 = object1 as Uri;
            Uri uri2 = object2 as Uri;

            if (uri1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (uri2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            if (uri1.OriginalString != uri2.OriginalString)
            {
                Differences.Add(new Difference(breadCrumb, NiceString(object1), NiceString(object2)));
            }
        }

        private void CompareType(object object1, object object2, string breadCrumb)
        {
            Type t1 = (Type)object1;
            Type t2 = (Type)object2;

            if (t1.FullName != t2.FullName)
            {
                Differences.Add(new Difference(breadCrumb, t1.FullName, t2.FullName));
            }
        }

#if !SILVERLIGHT
        private void CompareDataRow(object object1, object object2, string breadCrumb)
        {
            DataRow dataRow1 = object1 as DataRow;
            DataRow dataRow2 = object2 as DataRow;

            if (dataRow1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (dataRow2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            for (int i = 0; i < dataRow1.Table.Columns.Count; i++)
            {
                if (ElementsToInclude.Count > 0 && !ElementsToInclude.Contains(dataRow1.Table.Columns[i].ColumnName))
                    continue;
                
                //If we should ignore it, skip it
                if (ElementsToInclude.Count == 0 && ElementsToIgnore.Contains(dataRow1.Table.Columns[i].ColumnName))
                    continue;

                //If we should ignore read only, skip it
                if (!CompareReadOnly && dataRow1.Table.Columns[i].ReadOnly)
                    continue;

                //Both are null
                if (dataRow1.IsNull(i) && dataRow2.IsNull(i))
                    continue;

                string currentBreadCrumb = AddBreadCrumb(breadCrumb, String.Empty, String.Empty, dataRow1.Table.Columns[i].ColumnName);

                //Check if one of them is null
                if (dataRow1.IsNull(i)) 
                {
                    Differences.Add(new Difference(breadCrumb, null, NiceString(object2), dataRow1.Table.Columns[i].ColumnName));
                    return;
                }

                if (dataRow2.IsNull(i))
                {
                    Differences.Add(new Difference(breadCrumb, NiceString(object1), null, dataRow1.Table.Columns[i].ColumnName));
                    return;
                }

                Compare(dataRow1[i], dataRow2[i], currentBreadCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }

        private void CompareDataTable(object object1, object object2, string breadCrumb)
        {
            DataTable dataTable1 = object1 as DataTable;
            DataTable dataTable2 = object2 as DataTable;

            if (dataTable1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (dataTable2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            if (ElementsToInclude.Count > 0 && !ElementsToInclude.Contains(dataTable1.TableName))
                return;

            //If we should ignore it, skip it
            if (ElementsToInclude.Count == 0 && ElementsToIgnore.Contains(dataTable1.TableName))
                return;

            //There must be the same amount of rows in the datatable
            if (dataTable1.Rows.Count != dataTable2.Rows.Count)
            {
                Differences.Add(new Difference(breadCrumb, dataTable1.Rows.Count.ToString(), dataTable2.Rows.Count.ToString(), "Rows.Count"));

                if (Differences.Count >= MaxDifferences)
                    return;
            }

            //There must be the same amount of columns in the datatable
            if (dataTable1.Columns.Count != dataTable2.Columns.Count)
            {
                Differences.Add(new Difference(breadCrumb, dataTable1.Columns.Count.ToString(), dataTable2.Columns.Count.ToString(), "Columns.Count"));

                if (Differences.Count >= MaxDifferences)
                    return;
            }

            for (int i = 0; i < dataTable1.Rows.Count; i++)
            {
                string currentBreadCrumb = AddBreadCrumb(breadCrumb, "Rows", String.Empty, i);

                CompareDataRow(dataTable1.Rows[i], dataTable2.Rows[i], currentBreadCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }

        private void CompareDataset(object object1, object object2, string breadCrumb)
        {
            DataSet dataSet1 = object1 as DataSet;
            DataSet dataSet2 = object2 as DataSet;

            if (dataSet1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (dataSet2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");


            //There must be the same amount of tables in the dataset
            if (dataSet1.Tables.Count != dataSet2.Tables.Count)
            {
                Differences.Add(new Difference(breadCrumb, dataSet1.Tables.Count.ToString(), dataSet2.Tables.Count.ToString(), "Tables.Count"));
                if (Differences.Count >= MaxDifferences)
                    return;
            }

            for (int i = 0; i < dataSet1.Tables.Count; i++)
            {
                string currentBreadCrumb = AddBreadCrumb(breadCrumb, "Tables", String.Empty, dataSet1.Tables[i].TableName);

                CompareDataTable(dataSet1.Tables[i], dataSet2.Tables[i], currentBreadCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }
#endif

        /// <summary>
        /// Compare a timespan struct
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareTimespan(object object1, object object2, string breadCrumb)
        {
            if (((TimeSpan)object1).Ticks != ((TimeSpan)object2).Ticks)
            {
                Differences.Add(new Difference(breadCrumb, null, null, "Ticks"));
            }
        }

        /// <summary>
        /// Compare a pointer struct
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void ComparePointer(object object1, object object2, string breadCrumb)
        {
            if (
                (object1.GetType() == typeof(IntPtr) && object2.GetType() == typeof(IntPtr) && ((IntPtr)object1) != ((IntPtr)object2)) ||
                (object1.GetType() == typeof(UIntPtr) && object2.GetType() == typeof(UIntPtr) && ((UIntPtr)object1) != ((UIntPtr)object2))
                )
            {
                Differences.Add(new Difference(breadCrumb, null, null));
            }
        }

        /// <summary>
        /// Compare an enumeration
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareEnum(object object1, object object2, string breadCrumb)
        {
            if (object1.ToString() != object2.ToString())
            {
                Differences.Add(new Difference(breadCrumb, NiceString(object1), NiceString(object2), object1.GetType().Name));
            }
        }

        /// <summary>
        /// Compare a simple type
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareSimpleType(object object1, object object2, string breadCrumb)
        {
            if (object2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            IComparable valOne = object1 as IComparable;

            if (valOne == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (valOne.CompareTo(object2) != 0)
            {
                Differences.Add(new Difference(breadCrumb, NiceString(object1), NiceString(object2)));
            }
        }



        /// <summary>
        /// Compare a struct
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareStruct(object object1, object object2, string breadCrumb)
        {
            try
            {
                _parents.Add(object1);
                _parents.Add(object2);

                Type t1 = object1.GetType();

                PerformCompareFields(t1, object1, object2, true, breadCrumb);
                PerformCompareProperties(t1, object1, object2, true, breadCrumb);
            }
            finally
            {
                _parents.Remove(object1);
                _parents.Remove(object2);
            }
        }

        /// <summary>
        /// Compare the properties, fields of a class
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareClass(object object1, object object2, string breadCrumb)
        {
            try
            {
                _parents.Add(object1);
                _parents.Add(object2);

                Type t1 = object1.GetType();
                
                if (ElementsToInclude.Count > 0 && !ElementsToInclude.Contains(t1.Name))
                    return;

                //We ignore the class name
                if ((ElementsToInclude.Count == 0 && ElementsToIgnore.Contains(t1.Name)) || IgnoredByAttribute(t1))
                    return;

                //Compare the properties
                if (CompareProperties)
                    PerformCompareProperties(t1, object1, object2, false, breadCrumb);

                //Compare the fields
                if (CompareFields)
                    PerformCompareFields(t1, object1, object2, false, breadCrumb);
            }
            finally
            {
                _parents.Remove(object1);
                _parents.Remove(object2);
            }
        }


        /// <summary>
        /// Compare the fields of a class
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="structCompare"></param>
        /// <param name="breadCrumb"></param>
        private void PerformCompareFields(Type t1,
            object object1,
            object object2,
            bool structCompare,
            string breadCrumb)
        {
            IEnumerable<FieldInfo> currentFields = GetFieldInfo(t1);

            foreach (FieldInfo item in currentFields)
            {
                //Ignore invalid struct fields
                if (structCompare && !ValidStructSubType(item.FieldType))
                    continue;

                //Skip if this is a shallow compare
                if (!CompareChildren && IsChildType(item.FieldType))
                    continue;

                if (ElementsToInclude.Count > 0 && !ElementsToInclude.Contains(item.Name))
                    continue;

                //If we should ignore it, skip it
                if ((ElementsToInclude.Count == 0 && ElementsToIgnore.Contains(item.Name)) || IgnoredByAttribute(item.FieldType))
                    continue;

                object objectValue1 = item.GetValue(object1);
                object objectValue2 = item.GetValue(object2);

                bool object1IsParent = objectValue1 != null && (objectValue1 == object1 || _parents.Contains(objectValue1));
                bool object2IsParent = objectValue2 != null && (objectValue2 == object2 || _parents.Contains(objectValue2));

                //Skip fields that point to the parent
                if (IsClass(item.FieldType)
                    && (object1IsParent || object2IsParent))
                {
                    continue;
                }

                string currentCrumb = AddBreadCrumb(breadCrumb, item.Name, String.Empty, -1);

                Compare(objectValue1, objectValue2, currentCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }

        private IEnumerable<FieldInfo> GetFieldInfo(Type type)
        {
            if (Caching && _fieldCache.ContainsKey(type))
                return _fieldCache[type];

            FieldInfo[] currentFields;

#if !SILVERLIGHT
            if (ComparePrivateFields && !CompareStaticFields)
                currentFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            else if (ComparePrivateFields && CompareStaticFields)
                currentFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            else
#endif
                currentFields = type.GetFields(); //Default is public instance and static

            if (Caching)
                _fieldCache.Add(type, currentFields);

            return currentFields;
        }


        /// <summary>
        /// Compare the properties of a class
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="structCompare"></param>
        /// <param name="breadCrumb"></param>
        private void PerformCompareProperties(Type t1,
            object object1,
            object object2,
            bool structCompare,
            string breadCrumb)
        {
            IEnumerable<PropertyInfo> currentProperties = GetPropertyInfo(t1);

            foreach (PropertyInfo info in currentProperties)
            {
                //Ignore invalid struct fields
                if (structCompare && !ValidStructSubType(info.PropertyType))
                    continue;

                //If we can't read it, skip it
                if (info.CanRead == false)
                    continue;

                //Skip if this is a shallow compare
                if (!CompareChildren && IsChildType(info.PropertyType))
                    continue;
                
                if (ElementsToInclude.Count > 0 && !ElementsToInclude.Contains(info.Name))
                    continue;

                //If we should ignore it, skip it
                if ((ElementsToInclude.Count == 0 && ElementsToIgnore.Contains(info.Name)) || IgnoredByAttribute(info.PropertyType))
                    continue;

                //If we should ignore read only, skip it
                if (!CompareReadOnly && info.CanWrite == false)
                    continue;

                //If we ignore types then we must get correct PropertyInfo object
                PropertyInfo secondObjectInfo = null;
                if (IgnoreObjectTypes)
                {
                    var secondObjectPropertyInfos = GetPropertyInfo(object2.GetType());

                    foreach (var propertyInfo in secondObjectPropertyInfos)
                    {
                        if (propertyInfo.Name != info.Name) continue;

                        secondObjectInfo = propertyInfo;
                        break;
                    }
                }
                else
                    secondObjectInfo = info;

                object objectValue1;
                object objectValue2;
                if (!IsValidIndexer(info, breadCrumb))
                {
                    objectValue1 = info.GetValue(object1, null);
                    objectValue2 = secondObjectInfo != null ? secondObjectInfo.GetValue(object2, null) : null;
                }
                else
                {
                    CompareIndexer(info, object1, object2, breadCrumb);
                    continue;
                }

                bool object1IsParent = objectValue1 != null && (objectValue1 == object1 || _parents.Contains(objectValue1));
                bool object2IsParent = objectValue2 != null && (objectValue2 == object2 || _parents.Contains(objectValue2));

                //Skip properties where both point to the corresponding parent
                if ((IsClass(info.PropertyType) || IsStruct(info.PropertyType)) && (object1IsParent && object2IsParent))
                {
                    continue;
                }

                string currentCrumb = AddBreadCrumb(breadCrumb, info.Name, String.Empty, -1);

                Compare(objectValue1, objectValue2, currentCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }

        private IEnumerable<PropertyInfo> GetPropertyInfo(Type type)
        {
            if (Caching && _propertyCache.ContainsKey(type))
                return _propertyCache[type];

            PropertyInfo[] currentProperties;

#if SILVERLIGHT
            if (!CompareStaticProperties)
                currentProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            else
                currentProperties = type.GetProperties(); //Default is public instance and static
#else
            if (ComparePrivateProperties && !CompareStaticProperties)
                currentProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            else if (ComparePrivateProperties && CompareStaticProperties)
                currentProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            else if (!CompareStaticProperties)
                currentProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            else
                currentProperties = type.GetProperties(); //Default is public instance and static
#endif

            if (Caching)
                _propertyCache.Add(type, currentProperties);

            return currentProperties;
        }

        private void CompareIndexer(PropertyInfo info, object object1, object object2, string breadCrumb)
        {
            string currentCrumb;
            int indexerCount1 = (int)info.ReflectedType.GetProperty("Count").GetGetMethod().Invoke(object1, new object[] { });
            int indexerCount2 = (int)info.ReflectedType.GetProperty("Count").GetGetMethod().Invoke(object2, new object[] { });

            //Indexers must be the same length
            if (indexerCount1 != indexerCount2)
            {
                currentCrumb = AddBreadCrumb(breadCrumb, info.Name, String.Empty, -1);
                Differences.Add(new Difference(breadCrumb, indexerCount1.ToString(), indexerCount2.ToString(), info.Name + ".Count"));
                if (Differences.Count >= MaxDifferences)
                    return;
            }

            // Run on indexer
            for (int i = 0; i < indexerCount1; i++)
            {
                currentCrumb = AddBreadCrumb(breadCrumb, info.Name, String.Empty, i);
                object objectValue1 = info.GetValue(object1, new object[] { i });
                object objectValue2 = info.GetValue(object2, new object[] { i });
                Compare(objectValue1, objectValue2, currentCrumb);

                if (Differences.Count >= MaxDifferences)
                    return;
            }
        }

        /// <summary>
        /// Compare a dictionary
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareIDictionary(object object1, object object2, string breadCrumb)
        {
            IDictionary iDict1 = object1 as IDictionary;
            IDictionary iDict2 = object2 as IDictionary;

            if (iDict1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (iDict2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            try
            {
                _parents.Add(object1);
                _parents.Add(object2);

                //Objects must be the same length
                if (iDict1.Count != iDict2.Count)
                {
                    Differences.Add(new Difference(breadCrumb, iDict1.Count.ToString(), iDict2.Count.ToString(), ".Count"));

                    if (Differences.Count >= MaxDifferences)
                        return;
                }

                IDictionaryEnumerator enumerator1 = iDict1.GetEnumerator();
                IDictionaryEnumerator enumerator2 = iDict2.GetEnumerator();

                while (enumerator1.MoveNext() && enumerator2.MoveNext())
                {
                    string currentBreadCrumb = AddBreadCrumb(breadCrumb, "Key", String.Empty, -1);

                    Compare(enumerator1.Key, enumerator2.Key, currentBreadCrumb);

                    if (Differences.Count >= MaxDifferences)
                        return;

                    currentBreadCrumb = AddBreadCrumb(breadCrumb, "Value", String.Empty, -1);

                    Compare(enumerator1.Value, enumerator2.Value, currentBreadCrumb);

                    if (Differences.Count >= MaxDifferences)
                        return;
                }
            }
            finally
            {
                _parents.Remove(object1);
                _parents.Remove(object2);
            }
        }

        /// <summary>
        /// Compare an array or something that implements IList
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <param name="breadCrumb"></param>
        private void CompareIList(object object1, object object2, string breadCrumb)
        {
            IList ilist1 = object1 as IList;
            IList ilist2 = object2 as IList;

            if (ilist1 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object1");

            if (ilist2 == null) //This should never happen, null check happens one level up
                throw new ArgumentNullException("object2");

            try
            {
                _parents.Add(object1);
                _parents.Add(object2);

                //Objects must be the same length
                if (ilist1.Count != ilist2.Count)
                {
                    Differences.Add(new Difference(breadCrumb, ilist1.Count.ToString(), ilist2.Count.ToString(), ".Count"));

                    if (Differences.Count >= MaxDifferences)
                        return;
                }

                IEnumerator enumerator1 = ilist1.GetEnumerator();
                IEnumerator enumerator2 = ilist2.GetEnumerator();
                int count = 0;

                while (enumerator1.MoveNext() && enumerator2.MoveNext())
                {
                    string currentBreadCrumb = AddBreadCrumb(breadCrumb, String.Empty, String.Empty, count);

                    Compare(enumerator1.Current, enumerator2.Current, currentBreadCrumb);

                    if (Differences.Count >= MaxDifferences)
                        return;

                    count++;
                }
            }
            finally
            {
                _parents.Remove(object1);
                _parents.Remove(object2);
            }
        }


        private void CompareHashSet(object object1, object object2, string breadCrumb)
        {
            try
            {
                _parents.Add(object1);
                _parents.Add(object2);

                Type t1 = object1.GetType();

                //Get count by reflection since we can't cast it to HashSet<>
                int hashSet1Count = (int) GetPropertyValue(t1, object1, "Count");
                int hashSet2Count = (int)GetPropertyValue(t1, object2, "Count");

                //Objects must be the same length
                if (hashSet1Count != hashSet2Count)
                {
                    Differences.Add(new Difference(breadCrumb, hashSet1Count.ToString(), hashSet2Count.ToString(), ".Count"));

                    if (Differences.Count >= MaxDifferences)
                        return;
                }

                //Get enumerators by reflection
                MethodInfo methodInfo = GetMethod(t1, "GetEnumerator");
                IEnumerator enumerator1 = (IEnumerator) methodInfo.Invoke(object1, null);
                IEnumerator enumerator2 = (IEnumerator) methodInfo.Invoke(object2, null);

                int count = 0;

                while (enumerator1.MoveNext() && enumerator2.MoveNext())
                {
                    string currentBreadCrumb = AddBreadCrumb(breadCrumb, String.Empty, String.Empty, count);

                    Compare(enumerator1.Current, enumerator2.Current, currentBreadCrumb);

                    if (Differences.Count >= MaxDifferences)
                        return;

                    count++;
                }
            }
            finally
            {
                _parents.Remove(object1);
                _parents.Remove(object2);
            }
        }
        #endregion

        #region IsType methods
        private bool IsTypeOfType(Type type)
        {
            return (typeof(Type).IsAssignableFrom(type));
        }

        /// <summary>
        /// Check if any type has attributes that should be bypassed
        /// </summary>
        /// <returns></returns>
        private bool IgnoredByAttribute(Type type)
        {
            return AttributesToIgnore.Any(attributeType => type.GetCustomAttributes(attributeType, false).Length > 0);
        }

        private bool IsTimespan(Type type)
        {
            return type == typeof(TimeSpan);
        }

        private bool IsPointer(Type type)
        {
            return type == typeof(IntPtr) || type == typeof(UIntPtr);
        }

        private bool IsEnum(Type type)
        {
            return type.IsEnum;
        }

        private bool IsStruct(Type type)
        {
            return type.IsValueType && !IsSimpleType(type);
        }

        private bool IsSimpleType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            return type.IsPrimitive
                   || type == typeof (DateTime)
                   || type == typeof (decimal)
                   || type == typeof (string)
                   || type == typeof (Guid);

        }

        private bool ValidStructSubType(Type type)
        {
            return IsSimpleType(type)
                || IsEnum(type)
                || IsArray(type)
                || IsClass(type)
                || IsIDictionary(type)
                || IsTimespan(type)
                || IsIList(type);
        }

        private bool IsArray(Type type)
        {
            return type.IsArray;
        }

        private bool IsClass(Type type)
        {
            return type.IsClass;
        }

        private bool IsIDictionary(Type type)
        {
            return (typeof(IDictionary).IsAssignableFrom(type)) ;
        }

#if !SILVERLIGHT
        private bool IsDataset(Type type)
        {
            return type == typeof(DataSet);
        }

        private bool IsDataRow(Type type)
        {
            return type == typeof(DataRow);
        }

        private bool IsDataTable(Type type)
        {
            return type == typeof(DataTable);
        }
#endif

        private bool IsIList(Type type)
        {
            return (typeof(IList).IsAssignableFrom(type));
        }

        private bool IsChildType(Type type)
        {
            return !IsSimpleType(type)
                && (IsClass(type)
                    || IsArray(type)
                    || IsIDictionary(type)
                    || IsIList(type)
                    || IsStruct(type)
                    || IsHashSet(type)
                    );
        }

        private bool IsUri(Type type)
        {
            return (typeof (Uri).IsAssignableFrom(type));
        }

        private bool IsHashSet(Type type)
        {
            return type.IsGenericType 
                && type.GetGenericTypeDefinition().Equals(typeof(HashSet<>));
        }

        #endregion

        #region Validity Checking
        private bool IsValidIndexer(PropertyInfo info, string breadCrumb)
        {
            ParameterInfo[] indexers = info.GetIndexParameters();

            if (indexers.Length == 0)
            {
                return false;
            }

            if (indexers.Length > 1)
            {
                throw new Exception("Cannot compare objects with more than one indexer for object " + breadCrumb);
            }

            if (indexers[0].ParameterType != typeof(Int32))
            {
                throw new Exception("Cannot compare objects with a non integer indexer for object " + breadCrumb);
            }

            if (info.ReflectedType.GetProperty("Count") == null)
            {
                throw new Exception("Indexer must have a corresponding Count property for object " + breadCrumb);
            }

            if (info.ReflectedType.GetProperty("Count").PropertyType != typeof(Int32))
            {
                throw new Exception("Indexer must have a corresponding Count property that is an integer for object " + breadCrumb);
            }

            return true;
        }
        #endregion

        #region Supporting Methods

        private object GetPropertyValue(Type type, object objectValue, string propertyName)
        {
            return GetPropertyInfo(type).First(o => o.Name == propertyName).GetValue(objectValue, null);
        }

        /// <summary>
        /// Get a method by name
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private MethodInfo GetMethod(Type type, string methodName)
        {
            return GetMethods(type).FirstOrDefault(m => m.Name == methodName);
        }

        /// <summary>
        /// Get the cached methods for a type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private MethodInfo[] GetMethods(Type type)
        {
            if (_methodList.ContainsKey(type))
                return _methodList[type];

            MethodInfo[] myMethodInfo = type.GetMethods();
            _methodList.Add(type, myMethodInfo);
            return myMethodInfo;
        }

        /// <summary>
        /// Convert an object to a nicely formatted string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private string NiceString(object obj)
        {
            try
            {
                if (obj == null)
                    return "(null)";

                if (obj == DBNull.Value)
                    return "System.DBNull.Value";

                return obj.ToString();
            }
            catch
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Add a breadcrumb to an existing breadcrumb
        /// </summary>
        /// <param name="existing"></param>
        /// <param name="name"></param>
        /// <param name="extra"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private string AddBreadCrumb(string existing, string name, string extra, int index)
        {
            return AddBreadCrumb(existing, name, extra, index >= 0 ? index.ToString() : null);
        }

        /// <summary>
        /// Add a breadcrumb to an existing breadcrumb
        /// </summary>
        /// <param name="existing"></param>
        /// <param name="name"></param>
        /// <param name="extra"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private string AddBreadCrumb(string existing, string name, string extra, string index)
        {
            bool useIndex = !String.IsNullOrEmpty(index);
            bool useName = name.Length > 0;
            StringBuilder sb = new StringBuilder();

            sb.Append(existing);

            if (useName)
            {
                if (!String.IsNullOrEmpty(existing))
                    sb.AppendFormat(".");

                sb.Append(name);
            }

            sb.Append(extra);

            if (useIndex)
            {
                int result = -1;
                sb.AppendFormat(Int32.TryParse(index, out result) ? "[{0}]" : "[\"{0}\"]", index);
            }

#if BREADCRUMB
            Console.WriteLine(sb.ToString());
#endif

            return sb.ToString();
        }
        #endregion
    }

    public class Difference {
        public Difference(string propertyName, string originalValue, string updatedValue, string childPropertyName = null, string messagePrefix = null) {
            PropertyName = propertyName;
            ChildPropertyName = childPropertyName;
            OriginalValue = originalValue;
            NewValue = updatedValue;
            MessagePrefix = messagePrefix;
        }

        public string PropertyName { get; set; }

        public string ChildPropertyName { get; set; }

        public string OriginalValue { get; set; }

        public string NewValue { get; set; }

        private string MessagePrefix { get; set; }

        public override string ToString() {
            string message;

            if (!String.IsNullOrEmpty(PropertyName)) {
                if (String.IsNullOrEmpty(ChildPropertyName))
                    message = String.Format("object1.{0} != object2.{0} ({1},{2})", PropertyName, OriginalValue, NewValue);
                else
                    message = String.Format("object1.{0}.{1} != object2.{0}.{1} ({2},{3})", PropertyName, ChildPropertyName, OriginalValue, NewValue);
            } else {
                message = String.Format("object1 != object2 ({0},{1})", OriginalValue, NewValue);
            }

            if (!String.IsNullOrEmpty(MessagePrefix))
                message = String.Format("{0}: {1}", MessagePrefix, message);

            return message;
        }
    }
}
