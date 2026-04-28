using DDDExample.Application.Interfaces;
using DDDExample.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;
using System.Drawing;
using System.Text;

namespace DDDExample.Application.Services;

public class TotpMfaService : IMfaService
{
    private readonly MfaSettings _settings;

    public TotpMfaService(IOptions<MfaSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateSecret()
    {
        byte[] secretKey = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(secretKey);
    }

    public string GenerateQrCode(string secret, string userEmail, string issuer)
    {
        var provisionUri = new OtpUri(
            OtpType.Totp,
            secret,
            userEmail,
            issuer,
            OtpHashMode.Sha1,
            _settings.Digits,
            _settings.Period);

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(provisionUri.ToString(), QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return Convert.ToBase64String(qrCodeImage);
    }

    public bool VerifyToken(string secret, string token)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(token, out _, new VerificationWindow(_settings.Window));
    }

    public List<string> GenerateBackupCodes()
    {
        var codes = new List<string>(); 
        for (int i = 0; i < _settings.BackupCodesCount; i++)
        {
            byte[] Key = KeyGeneration.GenerateRandomKey(8);
            codes.Add(Base32Encoding.ToString(Key).Substring(0, 8));
        }
        return codes;
    }
}