using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Clnxr.Core;

namespace Clnxr.Platform.Windows
{
    internal sealed class SignedRulePackEnvelope
    {
        public string schemaVersion { get; set; }
        public string keyId { get; set; }
        public string payload { get; set; }
        public string signature { get; set; }
    }

    /// <summary>
    /// Resultado de verificação de um pacote de regras assinado.
    /// Verificar a assinatura não escolhe nem baixa uma chave: o chamador
    /// precisa fornecer uma chave pública confiável por configuração de release.
    /// </summary>
    public sealed class SignedRulePackVerification
    {
        public SignedRulePackVerification(bool succeeded, string message, string keyId,
            string catalogVersion, IList<Rule> rules)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            KeyId = keyId ?? string.Empty;
            CatalogVersion = catalogVersion ?? string.Empty;
            Rules = new List<Rule>(rules ?? new List<Rule>()).AsReadOnly();
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string KeyId { get; private set; }
        public string CatalogVersion { get; private set; }
        public IList<Rule> Rules { get; private set; }
    }

    /// <summary>
    /// Verifica envelopes locais de regras com assinatura RSA/SHA-256 e só
    /// libera regras depois de validar assinatura, schema, IDs e riscos.
    /// Não implementa download, rotação de chave ou atualização automática.
    /// </summary>
    public sealed class SignedRulePackService
    {
        public const string EnvelopeSchemaVersion = "clnxr.rules.signed.v1";

        public SignedRulePackVerification Verify(string envelopeJson, RSACryptoServiceProvider verifier)
        {
            if (string.IsNullOrWhiteSpace(envelopeJson))
                return Failure("Envelope de regras assinado ausente.", string.Empty);
            if (verifier == null)
                return Failure("Chave pública de verificação ausente.", string.Empty);

            try
            {
                SignedRulePackEnvelope envelope = new JavaScriptSerializer().Deserialize<SignedRulePackEnvelope>(envelopeJson);
                if (envelope == null || !string.Equals(envelope.schemaVersion, EnvelopeSchemaVersion, StringComparison.Ordinal))
                    return Failure("Schema de envelope de regras assinado não suportado.", envelope == null ? string.Empty : envelope.keyId);
                if (string.IsNullOrWhiteSpace(envelope.keyId) || string.IsNullOrWhiteSpace(envelope.payload) || string.IsNullOrWhiteSpace(envelope.signature))
                    return Failure("Envelope de regras assinado incompleto.", envelope.keyId);

                byte[] payload = Convert.FromBase64String(envelope.payload);
                byte[] signature = Convert.FromBase64String(envelope.signature);
                using (SHA256Managed hash = new SHA256Managed())
                {
                    if (!verifier.VerifyData(payload, hash, signature))
                        return Failure("Assinatura do pacote de regras inválida.", envelope.keyId);
                }

                string payloadJson = Encoding.UTF8.GetString(payload);
                WindowsRulePackDocument document = new JavaScriptSerializer().Deserialize<WindowsRulePackDocument>(payloadJson);
                IList<WindowsRuleTemplate> templates = WindowsRulePack.BuildTemplates(document);
                return new SignedRulePackVerification(true, "Assinatura e conteúdo do pacote confirmados localmente.",
                    envelope.keyId, document.catalogVersion, templates.Select(template => template.Rule).ToList());
            }
            catch (FormatException ex)
            {
                return Failure("Base64 de assinatura ou payload inválido: " + ex.Message, string.Empty);
            }
            catch (Exception ex)
            {
                return Failure("Pacote assinado rejeitado: " + ex.Message, string.Empty);
            }
        }

        public SignedRulePackVerification VerifyFile(string path, RSACryptoServiceProvider verifier)
        {
            if (string.IsNullOrWhiteSpace(path)) return Failure("Caminho do pacote ausente.", string.Empty);
            if (!File.Exists(path)) return Failure("Pacote de regras inexistente.", string.Empty);
            try { return Verify(File.ReadAllText(path, Encoding.UTF8), verifier); }
            catch (Exception ex) { return Failure("Não foi possível ler o pacote assinado: " + ex.Message, string.Empty); }
        }

        private static SignedRulePackVerification Failure(string message, string keyId)
        {
            return new SignedRulePackVerification(false, message, keyId, string.Empty, null);
        }
    }
}
