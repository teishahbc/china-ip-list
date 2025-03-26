# Mainland_China_IP_list_with_ipv4_v6

## 根据亚太互联网络信息中心（Asia-Pacific Network Information Centre，全球五大区域性互联网注册管理机构之一缩写作 APNIC）的亚太地区IP地址分配列表数据，更新IP数据。

### 每小时更新中国IP范围列表，Update Mainland China ip's list in every 1 hour

***************IPV4***************
路由器使用（Openwrt）直接访问 https://raw.githubusercontent.com/mayaxcn/china-ip-list/master/chnroute.txt <br>
其他客户端使用直接访问 https://raw.githubusercontent.com/mayaxcn/china-ip-list/master/chn_ip.txt

***************IPV6***************
路由器使用（Openwrt）直接访问 https://raw.githubusercontent.com/mayaxcn/china-ip-list/master/chnroute_v6.txt <br>
其他客户端使用直接访问 https://raw.githubusercontent.com/mayaxcn/china-ip-list/master/chn_ip_v6.txt


已修改 Program.cs 代码，使其从 APNIC 数据中筛选出仅属于指定自治系统（AS）号的中国的 IPv4 和 IPv6 地址范围。指定的 AS 号包括：AS4134（中国电信）、AS4808（中国移动）、AS4837（中国联通）、AS9808（CERNET）和 AS4812。
