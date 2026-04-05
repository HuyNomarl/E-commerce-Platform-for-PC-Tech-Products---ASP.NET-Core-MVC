using Eshop.Models.VNPay;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Eshop.Libraries
{
    public class VnPayLibrary
    {
        private readonly SortedDictionary<string, string> _requestData = new(StringComparer.Ordinal);
        private readonly SortedDictionary<string, string> _responseData = new(StringComparer.Ordinal);

        public PaymentResponseModel GetFullResponseData(IQueryCollection collection, string hashSecret)
        {
            var vnPay = new VnPayLibrary();

            foreach (var (key, value) in collection)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_", StringComparison.Ordinal))
                {
                    vnPay.AddResponseData(key, value.ToString());
                }
            }

            var orderIdRaw = vnPay.GetResponseData("vnp_TxnRef");
            var vnPayTranIdRaw = vnPay.GetResponseData("vnp_TransactionNo");
            var vnpResponseCode = vnPay.GetResponseData("vnp_ResponseCode");
            var vnpTransactionStatus = vnPay.GetResponseData("vnp_TransactionStatus");
            var orderInfo = vnPay.GetResponseData("vnp_OrderInfo");
            var amountRaw = vnPay.GetResponseData("vnp_Amount");
            var secureHash = collection.FirstOrDefault(x => x.Key == "vnp_SecureHash").Value.ToString();
            decimal amount = 0m;

            if (decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
            {
                amount = parsedAmount / 100m;
            }

            var checkSignature = vnPay.ValidateSignature(secureHash, hashSecret);
            var isSuccess = checkSignature && vnpResponseCode == "00" && vnpTransactionStatus == "00";

            return new PaymentResponseModel
            {
                Success = isSuccess,
                SignatureValid = checkSignature,
                PaymentMethod = "VnPay",
                OrderDescription = orderInfo,
                OrderId = orderIdRaw,
                PaymentId = vnPayTranIdRaw,
                TransactionId = vnPayTranIdRaw,
                Token = secureHash,
                VnPayResponseCode = vnpResponseCode,
                TransactionStatus = vnpTransactionStatus,
                Amount = amount
            };
        }

        public string GetIpAddress(HttpContext context)
        {
            try
            {
                var ip = context.Connection.RemoteIpAddress;

                if (ip == null)
                {
                    return "127.0.0.1";
                }

                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    ip = Dns.GetHostEntry(ip).AddressList
                        .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork) ?? ip;
                }

                return ip.ToString();
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _requestData[key] = value;
            }
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _responseData[key] = value;
            }
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
        }

        public string BuildRequestDataString()
        {
            return BuildDataString(_requestData);
        }

        public string ComputeRequestHash(string vnpHashSecret)
        {
            return HmacSha512(vnpHashSecret, BuildRequestDataString());
        }

        public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
        {
            var queryString = BuildRequestDataString(); // đã encode

            // HASH TRÊN CHUỖI ENCODE
            var vnpSecureHash = HmacSha512(vnpHashSecret, queryString);

            Console.WriteLine("HASH_DATA=" + queryString);
            Console.WriteLine("HASH=" + vnpSecureHash);

            return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
        }


        public bool ValidateSignature(string inputHash, string secretKey)
        {
            if (string.IsNullOrWhiteSpace(inputHash) || string.IsNullOrWhiteSpace(secretKey))
            {
                return false;
            }

            var myChecksum = ComputeResponseHash(secretKey);
            return myChecksum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        private static string HmacSha512(string key, string inputData)
        {
            var hash = new StringBuilder();
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using var hmac = new HMACSHA512(keyBytes);
            var hashValue = hmac.ComputeHash(inputBytes);
            foreach (var theByte in hashValue)
            {
                hash.Append(theByte.ToString("x2"));
            }

            return hash.ToString();
        }

        public string BuildResponseValidationDataString()
        {
            var responseData = _responseData
                .Where(kv =>
                    !string.IsNullOrEmpty(kv.Value) &&
                    !string.Equals(kv.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(kv.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase));

            return BuildDataString(responseData);
        }

        public string ComputeResponseHash(string secretKey)
        {
            return HmacSha512(secretKey, BuildResponseValidationDataString());
        }

        private static string BuildDataString(IEnumerable<KeyValuePair<string, string>> data)
        {
            return string.Join("&",
                data.Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        }

        //private static string BuildHashData(IEnumerable<KeyValuePair<string, string>> data)
        //{
        //    return string.Join("&",
        //        data.Where(kv => !string.IsNullOrEmpty(kv.Value))
        //            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
        //            .Select(kv => $"{kv.Key}={kv.Value}"));
        //}

        private static string HmacSha256(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using var hmac = new HMACSHA256(keyBytes);
            var hashValue = hmac.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            foreach (var b in hashValue)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
