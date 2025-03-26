using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics;

namespace china_ip_list
{
    class Program
    {
        public static string chn_ip = "", chnroute = "", chn_ip_v6 = "", chnroute_v6 = "";
        private static readonly HashSet<string> TargetASNs = new HashSet<string> { "AS4134", "AS4808", "AS4837", "AS9808", "AS4812" };
        private static Dictionary<string, string> ipToAsnMap = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            // 加载 IP-to-ASN 映射数据
            if (!LoadIpToAsnMap())
            {
                Console.WriteLine("IP-to-ASN 数据加载失败，程序退出。");
                Environment.Exit(1); // 非零退出码表示失败
            }

            string apnic_ip = GetResponse("http://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest");
            if (string.IsNullOrEmpty(apnic_ip))
            {
                Console.WriteLine("无法获取 APNIC 数据，请检查网络连接。");
                Environment.Exit(1);
            }

            string[] ip_list = apnic_ip.Split(new string[] { "\n" }, StringSplitOptions.None);
            int i = 0;
            int i_ip6 = 0;
            string save_txt_path = AppContext.BaseDirectory;

            foreach (string per_ip in ip_list)
            {
                if (per_ip.Contains("CN|ipv4|"))
                {
                    string[] ip_information = per_ip.Split('|');
                    string ip = ip_information[3];
                    int ip_count = Convert.ToInt32(ip_information[4]);
                    string ip_mask = Convert.ToString(32 - (Math.Log(ip_count) / Math.Log(2)));
                    string end_ip = IntToIp(IpToInt(ip) + (uint)ip_count - 1);

                    if (IsIpInTargetAsn(ip))
                    {
                        chnroute += ip + "/" + ip_mask + "\n";
                        chn_ip += ip + " " + end_ip + "\n";
                        i++;
                    }
                }

                if (per_ip.Contains("CN|ipv6|"))
                {
                    string[] ip_information_v6 = per_ip.Split('|');
                    string ip_v6 = ip_information_v6[3];
                    int ip_mask_v6 = Convert.ToInt32(ip_information_v6[4]);
                    string end_ip_v6 = CalculateEndIPv6Address(ip_v6, ip_mask_v6);

                    if (IsIpInTargetAsn(ip_v6))
                    {
                        chnroute_v6 += ip_v6 + "/" + ip_mask_v6 + "\n";
                        chn_ip_v6 += ip_v6 + " " + end_ip_v6 + "\n";
                        i_ip6++;
                    }
                }
            }

            File.WriteAllText(save_txt_path + "chnroute.txt", chnroute);
            File.WriteAllText(save_txt_path + "chn_ip.txt", chn_ip);
            Console.WriteLine("本次共获取" + i + "条CN IPv4的记录（目标AS），文件保存于" + save_txt_path + "chn_ip.txt");

            File.WriteAllText(save_txt_path + "chnroute_v6.txt", chnroute_v6);
            File.WriteAllText(save_txt_path + "chn_ip_v6.txt", chn_ip_v6);
            Console.WriteLine("本次共获取" + i_ip6 + "条CN IPv6的记录（目标AS），文件保存于" + save_txt_path + "chn_ip_v6.txt");
        }

        private static bool LoadIpToAsnMap()
        {
            string ipToAsnUrl = "https://iptoasn.com/data/ip2asn-v4.tsv";
            string ipToAsnData = GetResponse(ipToAsnUrl);
            if (string.IsNullOrEmpty(ipToAsnData))
            {
                Console.WriteLine("无法加载 IP-to-ASN 数据，URL: " + ipToAsnUrl);
                return false;
            }

            string[] lines = ipToAsnData.Split('\n');
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    string startIp = parts[0];
                    string asn = parts[2];
                    if (TargetASNs.Contains(asn))
                    {
                        ipToAsnMap[startIp,

] = asn;
                    }
                }
            }
            Console.WriteLine("已加载 IP-to-ASN 映射，包含 " + ipToAsnMap.Count + " 条目标 AS 记录。");
            return true;
        }

        private static bool IsIpInTargetAsn(string ip)
        {
            // 如果 ipToAsnMap 为空，直接返回 false
            if (ipToAsnMap.Count == 0) return false;

            // 验证 IP 格式并转换为整数
            if (!IsValidIPv4(ip)) return false;
            uint ipInt = IpToInt(ip);

            foreach (var entry in ipToAsnMap)
            {
                uint startIpInt = IpToInt(entry.Key);
                string[] parts = entry.Key.Split('.');
                int mask = 32 - (int)(Math.Log(256) / Math.Log(2)); // 简化为 /24
                uint range = (uint)(1 << (32 - mask));
                uint endIpInt = startIpInt + range - 1;

                if (ipInt >= startIpInt && ipInt <= endIpInt)
                {
                    return TargetASNs.Contains(entry.Value);
                }
            }
            return false;
        }

        private static string GetResponse(string url)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain")); // 修改为 text/plain 以匹配 TSV 文件
                try
                {
                    HttpResponseMessage response = httpClient.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        return response.Content.ReadAsStringAsync().Result;
                    }
                    else
                    {
                        Console.WriteLine($"HTTP 请求返回失败状态码: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HTTP 请求失败: {ex.Message}");
                }
                return null;
            }
        }

        private static bool IsValidIPv4(string ipStr)
        {
            if (string.IsNullOrEmpty(ipStr)) return false;
            string[] parts = ipStr.Split('.');
            if (parts.Length != 4) return false;
            foreach (string part in parts)
            {
                if (!byte.TryParse(part, out _)) return false;
            }
            return true;
        }

        private static uint IpToInt(string ipStr)
        {
            string[] ip = ipStr.Split('.');
            uint ipcode = 0xFFFFFF00 | byte.Parse(ip[3]);
            ipcode = ipcode & 0xFFFF00FF | (uint.Parse(ip[2]) << 0x08);
            ipcode = ipcode & 0xFF00FFFF | (uint.Parse(ip[1]) << 0x10);
            ipcode = ipcode & 0x00FFFFFF | (uint.Parse(ip[0]) << 0x18);
            return ipcode;
        }

        private static string IntToIp(uint ipcode)
        {
            byte addr1 = (byte)((ipcode & 0xFF000000) >> 0x18);
            byte addr2 = (byte)((ipcode & 0x00FF0000) >> 0x10);
            byte addr3 = (byte)((ipcode & 0x0000FF00) >> 0x08);
            byte addr4 = (byte)(ipcode & 0x000000FF);
            return string.Format("{0}.{1}.{2}.{3}", addr1, addr2, addr3, addr4);
        }

        private static BigInteger IpV6ToInt(string ipStr)
        {
            IPAddress ip = IPAddress.Parse(ipStr);
            List<byte> ipFormat = ip.GetAddressBytes().ToList();
            ipFormat.Reverse();
            ipFormat.Add(0);
            return new BigInteger(ipFormat.ToArray());
        }

        private static string DecimalToIpv6(BigInteger decimalValue)
        {
            string hexString = decimalValue.ToString("X");
            string paddedHexString = hexString.PadLeft(32, '0');
            string ipv6 = "";
            for (int i = 0; i < paddedHexString.Length; i += 4)
            {
                ipv6 += paddedHexString.Substring(i, 4) + ":";
            }
            ipv6 = ipv6.TrimEnd(':');
            return SimplifyIpv6Address(ipv6.ToLower());
        }

        private static string SimplifyIpv6Address(string ipv6)
        {
            string pattern = @"(?<![:\w])(?:0+:?){2,}(?![:\w])|(?:ffff:ffff(:?0+)?)+";
            string replacement = "::";
            string simplifiedIpAddress = Regex.Replace(ipv6, pattern, replacement);
            return simplifiedIpAddress.Replace(":0", ":");
        }

        public static string CalculateEndIPv6Address(string startIpAddress, int networkLength)
        {
            IPAddress ipAddress = IPAddress.Parse(startIpAddress);
            byte[] addressBytes = ipAddress.GetAddressBytes();
            int totalBits = 128;
            int subnetBits = totalBits - networkLength;
            BigInteger startAddress = IpV6ToInt(startIpAddress);
            BigInteger endAddress = startAddress + (BigInteger.One << subnetBits) - BigInteger.One;
            byte[] endAddressBytes = endAddress.ToByteArray();
            Array.Reverse(endAddressBytes);
            return new IPAddress(endAddressBytes).ToString();
        }
    }
}
