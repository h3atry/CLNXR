using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Clnxr.Core;

namespace Clnxr.Evidence
{
    public sealed class ReceiptFileVerification
    {
        public ReceiptFileVerification(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class ReceiptDetail
    {
        public ReceiptDetail(string section, string field, string value)
        {
            Section = section ?? string.Empty;
            Field = field ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Section { get; private set; }
        public string Field { get; private set; }
        public string Value { get; private set; }
    }

    public sealed class ReceiptDocument
    {
        public ReceiptDocument(string schemaVersion, IList<ReceiptDetail> details)
        {
            SchemaVersion = schemaVersion ?? string.Empty;
            Details = new List<ReceiptDetail>(details ?? new List<ReceiptDetail>());
        }

        public string SchemaVersion { get; private set; }
        public IList<ReceiptDetail> Details { get; private set; }
    }

    public sealed class ReceiptStore
    {
        private readonly string directory;
        private readonly JavaScriptSerializer serializer;

        public ReceiptStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Diretorio de recibos e obrigatorio.", "directory");
            this.directory = directory;
            serializer = new JavaScriptSerializer();
        }

        public static ReceiptStore CreateDefault()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CLNXR", "Receipts");
            return new ReceiptStore(root);
        }

        public string Save(CleanupReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException("receipt");
            Directory.CreateDirectory(directory);

            receipt.ReceiptHash = string.Empty;
            string unsignedPayload = serializer.Serialize(receipt);
            receipt.ReceiptHash = ComputeSha256(unsignedPayload);
            string finalPayload = serializer.Serialize(receipt);

            string name = receipt.CompletedUtc.ToString("yyyyMMdd-HHmmss") + "-" + receipt.ReceiptId + ".json";
            string finalPath = Path.Combine(directory, name);
            string temporaryPath = finalPath + ".tmp";
            File.WriteAllText(temporaryPath, finalPayload, new UTF8Encoding(false));
            File.Move(temporaryPath, finalPath);
            return finalPath;
        }

        public bool Verify(CleanupReceipt receipt)
        {
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.ReceiptHash)) return false;
            string originalHash = receipt.ReceiptHash;
            receipt.ReceiptHash = string.Empty;
            string payload = serializer.Serialize(receipt);
            receipt.ReceiptHash = originalHash;
            return string.Equals(originalHash, ComputeSha256(payload), StringComparison.OrdinalIgnoreCase);
        }

        public string SaveMaintenance(MaintenanceReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException("receipt");
            Directory.CreateDirectory(directory);

            receipt.ReceiptHash = string.Empty;
            string unsignedPayload = serializer.Serialize(receipt);
            receipt.ReceiptHash = ComputeSha256(unsignedPayload);
            string finalPayload = serializer.Serialize(receipt);

            string name = receipt.CompletedUtc.ToString("yyyyMMdd-HHmmss") + "-" + receipt.ToolId + "-" + receipt.ReceiptId + ".json";
            string finalPath = Path.Combine(directory, name);
            string temporaryPath = finalPath + ".tmp";
            File.WriteAllText(temporaryPath, finalPayload, new UTF8Encoding(false));
            File.Move(temporaryPath, finalPath);
            return finalPath;
        }

        public bool VerifyMaintenance(MaintenanceReceipt receipt)
        {
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.ReceiptHash)) return false;
            string originalHash = receipt.ReceiptHash;
            receipt.ReceiptHash = string.Empty;
            string payload = serializer.Serialize(receipt);
            receipt.ReceiptHash = originalHash;
            return string.Equals(originalHash, ComputeSha256(payload), StringComparison.OrdinalIgnoreCase);
        }

        public IList<string> ListReceiptPaths()
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public ReceiptFileVerification VerifyFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return new ReceiptFileVerification(false, "Caminho de recibo ausente.");
            if (!File.Exists(path)) return new ReceiptFileVerification(false, "Recibo local inexistente.");

            try
            {
                string payload = File.ReadAllText(path, Encoding.UTF8);
                serializer.DeserializeObject(payload);

                Match schemaMatch = Regex.Match(payload, "\\\"SchemaVersion\\\"\\s*:\\s*\\\"(?<schema>[^\\\"]+)\\\"", RegexOptions.CultureInvariant);
                if (!schemaMatch.Success) return new ReceiptFileVerification(false, "Recibo sem versão de esquema declarada.");

                Match hashMatch = Regex.Match(payload, "\\\"ReceiptHash\\\"\\s*:\\s*\\\"(?<hash>[a-fA-F0-9]{64})\\\"", RegexOptions.CultureInvariant);
                if (!hashMatch.Success) return new ReceiptFileVerification(false, "Recibo sem hash SHA-256 válido.");

                string storedHash = hashMatch.Groups["hash"].Value;
                string unsignedPayload = payload.Remove(hashMatch.Groups["hash"].Index, hashMatch.Groups["hash"].Length);
                bool valid = string.Equals(storedHash, ComputeSha256(unsignedPayload), StringComparison.OrdinalIgnoreCase);
                return valid
                    ? new ReceiptFileVerification(true, "Integridade SHA-256 confirmada localmente para " + schemaMatch.Groups["schema"].Value + ".")
                    : new ReceiptFileVerification(false, "O conteúdo do recibo não corresponde ao hash registrado.");
            }
            catch (Exception ex)
            {
                return new ReceiptFileVerification(false, "Não foi possível verificar o recibo: " + ex.Message);
            }
        }

        public ReceiptDocument ReadDocument(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho de recibo ausente.", "path");
            if (!File.Exists(path)) throw new FileNotFoundException("Recibo local inexistente.", path);

            string payload = File.ReadAllText(path, Encoding.UTF8);
            IDictionary document = serializer.DeserializeObject(payload) as IDictionary;
            if (document == null) throw new InvalidDataException("O recibo nao contem um objeto JSON valido.");

            List<ReceiptDetail> details = new List<ReceiptDetail>();
            object schema;
            string schemaVersion = document.Contains("SchemaVersion") && (schema = document["SchemaVersion"]) != null
                ? Convert.ToString(schema) : string.Empty;

            AddDetails(details, "Recibo", document, "Results");
            object results = document.Contains("Results") ? document["Results"] : null;
            IEnumerable resultItems = results as IEnumerable;
            if (resultItems != null && !(results is string))
            {
                int index = 0;
                foreach (object result in resultItems)
                {
                    index++;
                    IDictionary action = result as IDictionary;
                    if (action != null) AddDetails(details, "Resultado " + index, action, null);
                    else details.Add(new ReceiptDetail("Resultado " + index, "Valor", FormatValue(result)));
                }
            }

            return new ReceiptDocument(schemaVersion, details);
        }

        private static void AddDetails(ICollection<ReceiptDetail> destination, string section, IDictionary source, string excludedField)
        {
            List<DictionaryEntry> ordered = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in source)
            {
                string field = Convert.ToString(entry.Key);
                if (!string.Equals(field, excludedField, StringComparison.Ordinal)) ordered.Add(entry);
            }

            foreach (DictionaryEntry entry in ordered.OrderBy(entry => Convert.ToString(entry.Key), StringComparer.OrdinalIgnoreCase))
                destination.Add(new ReceiptDetail(section, Convert.ToString(entry.Key), FormatValue(entry.Value)));
        }

        private static string FormatValue(object value)
        {
            if (value == null) return string.Empty;
            string text = value as string;
            if (text != null) return text;
            IEnumerable list = value as IEnumerable;
            if (list != null) return "[colecao]";
            return Convert.ToString(value);
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder output = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash) output.Append(item.ToString("x2"));
                return output.ToString();
            }
        }
    }
}
