using System.Security.Cryptography;

namespace POS.Licensing.Cryptography;

public static class RsaKeyHelper
{
    public const int DefaultKeySize = 3072;

    public static (string PrivateKeyPem, string PublicKeyPem) GenerateKeyPair(int keySize = DefaultKeySize)
    {
        using var rsa = RSA.Create(keySize);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (privateKeyPem, publicKeyPem);
    }
}
