using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace china_ip_list
{
    class Program
    {
        public static string chn_ip = "", chnroute = "", chn_ip_v6 = "", chnroute_v6 = "";
        private static readonly HashSet<string> TargetASNs = new HashSet<string> { "AS4134", "AS4808", "AS4837", "AS9808", "AS4812" };
        private static Dictionary<uint, (uint EndIp, string Asn)> ipToAsnMap = new Dictionary<uint, (uint, string)>();

        static void Main(string[] args)
        {
            if (!LoadIpToAsnMap())
            {
                Console.WriteLine("IP-to-ASN 数据加载失败，程序退出。");
                Environment.Exit(1);
            }

            string apnic_ip = GetResponse("http://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest");
            if (string.IsNullOrEmpty(apnic_ip))
            {
                Console.WriteLine("无法获取 APNIC 数据，请检查网络连接。");
                Environment.Exit(1);
            }

            Console.WriteLine("成功获取 APNIC 数据，开始处理...");

            string[] ip_list = apnic_ip.Split(new string[] { "\n" }, StringSplitOptions.None);
            int i = 0;
            int i_ip6 = 0;
            string save_txt_path = AppContext.BaseDirectory;

            int total_ipv4_entries = 0;
            foreach (string per_ip in ip_list)
            {
                if (per_ip.Contains("CN|ipv4|"))
                {
                    total_ipv4_entries++;
                    string[] ip_information = per_ip.Split('|');
                    if (ip_information.Length < 5)
                    {
                        Console.WriteLine($"无效的 IPv4 数据行: {per_ip}");
                        continue;
                    }

                    string ip = ip_information[3];
                    int ip_count;
                    if (!int.TryParse(ip_information[4], out ip_count))
                    {
                        Console.WriteLine($"无法解析 IP 计数: {per_ip}");
                        continue;
                    }

                    string ip_mask = Convert.ToString(32 - (Math.Log(ip_count) / Math.Log(2)));
                    string end_ip = IntToIp(IpToInt(ip) + (uint)ip_count - 1);

                    if (IsIpInTargetAsn(ip))
                    {
                        chnroute += ip + "/" + ip_mask + "\n";
                        chn_ip += ip + " " + end_ip + "\n";
                        i++;
                        Console.WriteLine($"匹配目标 ASN: {ip} - {end_ip}");
                    }
                }

                if (per_ip.Contains("CN|ipv6|"))
                {
                    i_ip6++; // 统计 IPv6 条目数
                }
            }

            Console.WriteLine($"总共处理 {total_ipv4_entries} 条 CN IPv4 记录，匹配目标 ASN 的有 {i} 条");
            Console.WriteLine($"总共处理 {i_ip6} 条 CN IPv6 记录，匹配目标 ASN 的有 0 条（暂不支持 IPv6 ASN）");

            File.WriteAllText(save_txt_path + "chnroute.txt", chnroute);
            File.WriteAllText(save_txt_path + "chn_ip.txt", chn_ip);
            Console.WriteLine("本次共获取" + i + "条CN IPv4的记录（目标AS），文件保存于" + save_txt_path + "chn_ip.txt");

            File.WriteAllText(save_txt_path + "chnroute_v6.txt", chnroute_v6);
            File.WriteAllText(save_txt_path + "chn_ip_v6.txt", chn_ip_v6);
            Console.WriteLine("本次共获取0条CN IPv6的记录（目标AS），文件保存于" + save_txt_path + "chn_ip_v6.txt");
        }

        private static bool LoadIpToAsnMap()
        {
            string ipToAsnUrl = "https://github.com/pl-strflt/iptoasn/raw/main/data/ip2asn-v4.tsv.gz";
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/gzip"));
                    var response = httpClient.GetAsync(ipToAsnUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"HTTP 请求失败: {response.StatusCode}");
                        return false;
                    }

                    using (var stream = response.Content.ReadAsStreamAsync().Result)
                    using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzipStream))
                    {
                        string ipToAsnData = reader.ReadToEnd();
                        if (string.IsNullOrEmpty(ipToAsnData))
                        {
                            Console.WriteLine("IP-to-ASN 数据为空，URL: " + ipToAsnUrl);
                            return false;
                        }

                        Console.WriteLine($"成功下载 IP-to-ASN 数据，大小: {ipToAsnData.Length} 字符");

                        string[] lines = ipToAsnData.Split('\n');
                        Console.WriteLine($"总计 {lines.Length} 行数据，开始解析...");
                        int validLines = 0;

                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            string[] parts = line.Split('\t');
                            if (parts.Length < 3)
                            {
                                Console.WriteLine($"无效行: {line}");
                                continue;
                            }

                            string startIp = parts[0];
                            string endIp = parts[1];
                            string asn = parts[2];
                            validLines++;

                            if (TargetASNs.Contains(asn))
                            {
                                uint startIpInt = IpToInt(startIp);
                                uint endIpInt = IpToInt(endIp);
                                ipToAsnMap[startIpInt] = (endIpInt, asn);
                                Console.WriteLine($"加载目标 ASN: {startIp} - {endIp} -> {asn}");
                            }
                        }

                        Console.WriteLine($"解析完成，有效行数: {validLines}，匹配目标 ASN 的记录数: {ipToAsnMap.Count}");
                    }
                }
                if (ipToAsnMap.Count == 0)
                {
                    Console.WriteLine("警告: 未找到任何匹配目标 ASN 的记录，请检查数据源或目标 ASN 配置。");
                    return true; // 不退出程序，继续运行以观察后续行为
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载 IP-to-ASN 数据异常: {ex.Message}");
                return false;
            }
        }

        private static bool IsIpInTargetAsn(string ip)
        {
            if (ipToAsnMap.Count == 0)
            {
                Console.WriteLine("IP-to-ASN 映射为空，无法筛选");
                return false;
            }
            if (!IsValidIPv4(ip))
            {
                return false; // IPv6 不支持，直接返回 false
            }

            uint ipInt = IpToInt(ip);
            foreach (var entry in ipToAsnMap)
            {
                uint startIpInt = entry.Key;
                uint endIpInt = entry.Value.EndIp;
                if (ipInt >= startIpInt && ipInt <= endIpInt)
                {
                    return true; // 找到匹配，不打印过多日志
                }
            }
            return false;
        }

        private static string GetResponse(string url)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                try
                {
                    var response = httpClient.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        return response.Content.ReadAsStringAsync().Result;
                    }
                    Console.WriteLine($"HTTP 请求失败: {response.StatusCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"HTTP 请求异常: {ex.Message}");
                    return null;
                }
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
    }
}
