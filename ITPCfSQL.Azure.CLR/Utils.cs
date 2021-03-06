﻿using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Security.Cryptography.X509Certificates;

namespace ITPCfSQL.Azure.CLR
{
    public class Utils
    {
        [SqlFunction
        (IsDeterministic = true,
        IsPrecise = true,
        DataAccess = DataAccessKind.None,
        SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlDate(DateTime dt)
        {
            return System.Xml.XmlConvert.ToString(dt, System.Xml.XmlDateTimeSerializationMode.Utc);// dt.ToString("o");
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            string ret = str;
            ret = ret.Replace("'", "&apos;");
            ret = ret.Replace("\"", "&quot;");
            ret = ret.Replace(">", "&gt;");
            ret = ret.Replace("<", "&lt;");
            ret = ret.Replace("&", "&amp;");
            return ret;
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlInt64(Int64 i64)
        {
            return System.Xml.XmlConvert.ToString(i64);
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlDouble(double d)
        {
            return System.Xml.XmlConvert.ToString(d);
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlBinary(byte[] buffer)
        {
            StringBuilder sb = new StringBuilder(buffer.Length * 2);
            foreach (byte b in buffer)
                sb.AppendFormat("{0:X2}", b);
            return sb.ToString();
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string ToXmlGuid(Guid guid)
        {
            return ToXmlBinary(guid.ToByteArray());
        }

        [SqlFunction
        (IsDeterministic = false,
        IsPrecise = true,
        DataAccess = DataAccessKind.Read,
        SystemDataAccess = SystemDataAccessKind.Read)]
        public static string ToXmlStatement(string statement)
        {
            System.Data.DataTable dt = new System.Data.DataTable();

            using (System.Data.SqlClient.SqlConnection con = new System.Data.SqlClient.SqlConnection("context connection=true"))
            {
                SqlCommand cmd = new SqlCommand(statement, con);
                using (SqlDataAdapter ada = new SqlDataAdapter(cmd))
                {
                    ada.Fill(dt);
                }
            }

            StringBuilder sb = new StringBuilder();

            System.Xml.XmlWriterSettings xws = new System.Xml.XmlWriterSettings();
            xws.OmitXmlDeclaration = true;

            using (System.Xml.XmlWriter wr = System.Xml.XmlWriter.Create(sb, xws))
            {
                wr.WriteStartElement("ResultSet");

                for (int iRow = 0; iRow < dt.Rows.Count; iRow++)
                {
                    wr.WriteStartElement("Record");

                    for (int iCol = 0; iCol < dt.Columns.Count; iCol++)
                    {
                        wr.WriteStartElement(dt.Columns[iCol].ColumnName);

                        if (dt.Rows[iRow][iCol] != DBNull.Value)
                        {
                            if (dt.Rows[iRow][iCol] is Guid)
                                wr.WriteValue(((Guid)dt.Rows[iRow][iCol]).ToString());
                            else
                                wr.WriteValue(dt.Rows[iRow][iCol]);
                        }

                        wr.WriteEndElement();
                    }

                    wr.WriteEndElement();
                }

                wr.WriteEndElement();

                wr.Flush();
            }

            return sb.ToString();
        }


        [SqlFunction
        (IsDeterministic = true,
        IsPrecise = true,
        DataAccess = DataAccessKind.None,
        SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString ComputeMD5AsBase64(SqlBinary byteArray)
        {
            if (byteArray.IsNull)
                return SqlString.Null;

            System.Security.Cryptography.MD5 sscMD5 = System.Security.Cryptography.MD5.Create();
            byte[] mHash = sscMD5.ComputeHash(byteArray.Value);
            return Convert.ToBase64String(mHash);
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlInt64 GetFileSizeBytes(SqlString strFileName)
        {
            return new System.IO.FileInfo(strFileName.Value).Length;
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlBytes GetFileBlock(SqlString strFileName, SqlInt64 lOffsetBytes, SqlInt32 iLengthBytes,
            SqlString strFileShare)
        {
            System.IO.FileShare fileShare;
            if (!Enum.TryParse<System.IO.FileShare>(strFileShare.Value, out fileShare))
            {
                StringBuilder sb = new StringBuilder("Invalid System.IO.FileShare value. Received " +
                    strFileShare.Value + ". Valid values are: ");

                System.IO.FileShare[] fss = (System.IO.FileShare[])Enum.GetValues(typeof(System.IO.FileShare));

                for (int i = 0; i < fss.Length; i++)
                {
                    sb.Append(fss[i].ToString());
                    if ((i + 1) < fss.Length)
                        sb.Append(", ");
                }

                sb.Append(".");

                throw new ArgumentException(sb.ToString());
            }

            byte[] bBuffer = new byte[iLengthBytes.Value];

            using (System.IO.FileStream fs = new System.IO.FileStream(
                strFileName.Value, System.IO.FileMode.Open,
                System.IO.FileAccess.Read, fileShare,
                1024 * 64, System.IO.FileOptions.RandomAccess))
            {
                fs.Seek(lOffsetBytes.Value, System.IO.SeekOrigin.Begin);
                fs.Read(bBuffer, 0, iLengthBytes.Value);
            }

            return new SqlBytes(bBuffer);
        }

        #region Non exported methods
        internal static void PushSingleRecordResult(object result, System.Data.SqlDbType sqlDBType)
        {
            //SqlContext.Pipe.Send("Response output:\n");
            //SqlContext.Pipe.Send(result.ToString());

            SqlDataRecord record = null;

            switch (sqlDBType)
            {
                case System.Data.SqlDbType.NVarChar:
                case System.Data.SqlDbType.VarChar:
                    record = new SqlDataRecord(new SqlMetaData[] { new SqlMetaData("Result", sqlDBType, -1) });
                    record.SetString(0, result.ToString());
                    break;
                case System.Data.SqlDbType.Xml:
                    record = new SqlDataRecord(new SqlMetaData[] { new SqlMetaData("Result", sqlDBType) });

                    SqlXml xml;
                    using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(new System.IO.StringReader(result.ToString())))
                    {
                        xml = new SqlXml(reader);
                    }

                    record.SetSqlXml(0, xml);
                    break;
                case System.Data.SqlDbType.Int:
                    record = new SqlDataRecord(new SqlMetaData[] { new SqlMetaData("Result", sqlDBType) });
                    record.SetInt32(0, (Int32)result);
                    break;
                default:
                    throw new ArgumentException("SqlDbType " + sqlDBType.ToString() + " is not supported by PushSingleRecordResult.");
            }

            SqlContext.Pipe.SendResultsStart(record);
            SqlContext.Pipe.SendResultsRow(record);
            SqlContext.Pipe.SendResultsEnd();
        }
        #endregion

        #region Shared access signature
        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString GenerateBlobSharedAccessSignatureURI(
            SqlString resourceUri, SqlString sharedKey,
            SqlString permissions, SqlString resourceType,
            SqlDateTime validityStart, SqlDateTime validityEnd,
           SqlString identifier)
        {
            return ITPCfSQL.Azure.Internal.Signature.GenerateSharedAccessSignatureURI(
                new Uri(resourceUri.Value),
                sharedKey.Value,
                permissions.IsNull ? null : permissions.Value,
                resourceType.IsNull ? null : resourceType.Value,
                validityStart.IsNull ? (DateTime?)null : validityStart.Value,
                validityEnd.IsNull ? (DateTime?)null : validityEnd.Value,
                null, null,
                identifier.IsNull ? null : identifier.Value).AbsoluteUri;
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString GenerateDirectBlobSharedAccessSignatureURI(
            SqlString resourceUri, SqlString sharedKey,
            SqlString permissions, SqlString resourceType,
            SqlDateTime validityStart, SqlDateTime validityEnd)
        {
            return ITPCfSQL.Azure.Internal.Signature.GenerateSharedAccessSignatureURI(
                new Uri(resourceUri.Value),
                sharedKey.Value,
                permissions.IsNull ? null : permissions.Value,
                resourceType.IsNull ? null : resourceType.Value,
                validityStart.IsNull ? (DateTime?)null : validityStart.Value,
                validityEnd.IsNull ? (DateTime?)null : validityEnd.Value,
                null, null,
                null).AbsoluteUri;
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString GeneratePolicyBlobSharedAccessSignatureURI(
            SqlString resourceUri, SqlString sharedKey,
            SqlString resourceType,
           SqlString identifier)
        {
            return ITPCfSQL.Azure.Internal.Signature.GenerateSharedAccessSignatureURI(
                new Uri(resourceUri.Value),
                sharedKey.Value,
                null,
                resourceType.IsNull ? null : resourceType.Value,
                (DateTime?)null,
                (DateTime?)null,
                null, null,
                identifier.IsNull ? null : identifier.Value).AbsoluteUri;
        }
        #endregion

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString GetContainerFromUri(
            SqlString uri)
        {
            if (uri.IsNull)
                return null;
            return uri.Value.Split(new char[] { '/' })[3];
        }

        #region Blog methods
        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string NoAccess(string str)
        {
            return str.ToUpper();
        }

        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.Read,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static string WithAccess(string str)
        {
            return str.ToUpper();
        }
        #endregion

        #region Certificates
        [SqlFunction(
            DataAccess = DataAccessKind.None,
            FillRowMethodName = "_ListCertificatesCallback",
            IsDeterministic = false,
            IsPrecise = true,
            SystemDataAccess = SystemDataAccessKind.None,
            TableDefinition = "FriendlyName NVARCHAR(MAX), IssuerName NVARCHAR(MAX), SubjectName NVARCHAR(MAX), Thumbprint NVARCHAR(255), HasPrivateKey BIT, NotAfter DATETIME, NotBefore DATETIME, SerialNumber NVARCHAR(255), SignatureAlgorithm NVARCHAR(255), [Subject] NVARCHAR(255)")]
        public static System.Collections.IEnumerable ListCertificates()
        {
            X509Store certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);

            return certStore.Certificates;
        }
        public static void _ListCertificatesCallback(Object obj,
           out SqlString FriendlyName,
           out SqlString IssuerName,
           out SqlString SubjectName,
           out SqlString Thumbprint,
           out SqlBoolean HasPrivateKey,
           out SqlDateTime NotAfter,
           out SqlDateTime NotBefore,
           out SqlString SerialNumber,
           out SqlString SignatureAlgorithm,
           out SqlString Subject)
        {
            X509Certificate2 cert = obj as X509Certificate2;

            FriendlyName = cert.FriendlyName;
            IssuerName = cert.IssuerName.Name;
            SubjectName = cert.SubjectName.Name;
            Thumbprint = cert.Thumbprint;
            HasPrivateKey = cert.HasPrivateKey;
            NotAfter = cert.NotAfter;
            NotBefore = cert.NotBefore;
            SerialNumber = cert.SerialNumber;
            SignatureAlgorithm = cert.SignatureAlgorithm.FriendlyName;
            Subject = cert.Subject;
        }
        #endregion

        #region URI simple methods
        [SqlFunction
            (IsDeterministic = true,
            IsPrecise = true,
            DataAccess = DataAccessKind.None,
            SystemDataAccess = SystemDataAccessKind.None)]
        public static SqlString DownloadURI(SqlString URI)
        {
            System.Net.WebRequest req = System.Net.HttpWebRequest.Create(URI.ToString());
            req.Method = "GET";

            System.Net.WebResponse resp = req.GetResponse();

            using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream()))
            {
                return sr.ReadToEnd();
            }
        }
        #endregion
    }
}
