export interface SteamDownloadRegion {
  value: string;
  label: string;
}

// Generated from D:\Steam\public\steamui_schinese.txt (Steam client localization).
const STEAM_DOWNLOAD_REGION_DATA = `	使用 Steam 默认区域
chicago	美国 - 芝加哥
newyork	美国 - 纽约
london	英国 - 伦敦
frankfurt	德国 - 法兰克福
moscow	俄罗斯 - 莫斯科
seoul	韩国 - 首尔
taiwan	台湾
sanjose	美国 - 圣何塞
phoenix	美国 - 凤凰城
miami	美国 - 迈阿密
paris	法国 - 巴黎
amsterdam	荷兰
bucharest	罗马尼亚
toronto	加拿大 - 多伦多
auckland	新西兰
sao_paulo	巴西 - 圣保罗
johannesburg	南非 - 约翰内斯堡
iceland	冰岛、格陵兰、法罗群岛
israel	以色列
seattle	美国 - 西雅图
tokyo	日本 - 东京
hongkong	中国 - 香港
bangkok	泰国
singapore	新加坡
mumbai	印度 - 孟买
rome	意大利 - 罗马
warsaw	波兰 - 华沙
yekaterinburg	俄罗斯 - 叶卡捷琳堡
madrid	西班牙 - 马德里
copenhagen	丹麦
prague	捷克共和国
athens	希腊
jakarta	印度尼西亚 - 雅加达
manila	菲律宾 - 马尼拉
beijing	中国 - 北京
shanghai	中国 - 上海
chengdu	中国 - 成都
denver	美国 - 丹佛
atlanta	美国 - 亚特兰大
brisbane	澳大利亚 - 布里斯班
sydney	澳大利亚 - 悉尼
melbourne	澳大利亚 - 墨尔本
adelaide	澳大利亚 - 阿德莱德
perth	澳大利亚 - 珀斯
novosibirsk	俄罗斯 - 新西伯利亚
dc	美国 - 华盛顿特区
la	美国 - 洛杉矶
dallas	美国 - 达拉斯
stockholm	瑞典 - 斯德哥尔摩
oslo	挪威 - 奥斯陆
helsinki	芬兰 - 赫尔辛基
dublin	爱尔兰
cambodia	柬埔寨
vietnam	越南
kualalumpur	马来西亚 - 吉隆坡
kiev	乌克兰 - 基辅
sandiego	美国 - 圣地亚哥
sacramento	美国 - 萨克拉门托
minneapolis	美国 - 明尼阿波利斯
stlouis	美国 - 圣路易斯
houston	美国 - 休斯顿
detroit	美国 - 底特律
pittsburgh	美国 - 匹兹堡
montreal	加拿大 - 蒙特利尔
boston	美国 - 波士顿
philadelphia	美国 - 费城
charlotte	美国 - 夏洛特
manchester	英国 - 曼彻斯特
belgium	比利时
dusseldorf	德国 - 杜塞尔多夫
switzerland	瑞士
hamburg	德国 - 汉堡
berlin	德国 - 柏林
munich	德国 - 慕尼黑
vienna	奥地利
budapest	匈牙利
vancouver	加拿大 - 温哥华
columbus	美国 - 哥伦布
marseille	法国 - 马赛
capetown	南非 - 开普敦
mexicocity	墨西哥 - 墨西哥城
buenosaires	阿根廷 - 布宜诺斯艾利斯
santiago	智利 - 圣地亚哥
lima	秘鲁 - 利马
bogota	哥伦比亚 - 波哥大
istanbul	土耳其 - 伊斯坦布尔
cairo	埃及 - 开罗
riyadh	沙特阿拉伯 - 利雅得
dubai	阿拉伯联合酋长国
karachi	巴基斯坦 - 卡拉奇
luxembourg	卢森堡
lagos	非洲 - 西部
nairobi	非洲 - 东部
rabat	非洲 - 西北部
edmonton	加拿大 - 埃德蒙顿
calgary	加拿大 - 卡尔加里
winnipeg	加拿大 - 温尼伯
ottawa	加拿大 - 渥太华
panama	中美洲
puerto_rico	加勒比
chennai	印度 - 金奈
delhi	印度 - 德里
bangalore	印度 - 班加罗尔
hyderabad	印度 - 海得拉巴
kolkata	印度 - 加尔各答
honolulu	美国 - 檀香山
anchorage	美国 - 安克雷奇
guangzhou	中国 - 广州
stpetersburg	俄罗斯 - 圣彼得堡
rostov	俄罗斯 - 罗斯托夫州
kazan	俄罗斯 - 喀山
vladivostok	俄罗斯 - 符拉迪沃斯托克
recife	巴西 - 累西腓
brasilia	巴西 - 巴西利亚
rio	巴西 - 里约热内卢
portoalegre	巴西 - 阿雷格里港
irkutsk	俄罗斯 - 伊尔库茨克
kazakhstan	哈萨克斯坦 - 阿斯塔纳
almaty	哈萨克斯坦 - 阿拉木图
wuhan	中国 - 武汉
xian	中国 - 西安
mongolia	蒙古
venezuela	委内瑞拉
ecuador	厄瓜多尔
bolivia	玻利维亚
belarus	白俄罗斯
harbin	中国 - 哈尔滨
kunming	中国 - 昆明
qingdao	中国 - 青岛
urumqi	中国 - 乌鲁木齐
zhengzhou	中国 - 郑州
changsha	中国 - 长沙
caucasus	高加索
central_asia	中亚
pacific_islands	太平洋岛国
malmo	瑞典 - 马尔默
goteborg	瑞典 - 哥德堡
sapporo	日本 - 札幌
sendai	日本 - 仙台
nagoya	日本 - 名古屋
osaka	日本 - 大阪
fukuoka	日本 - 福冈
busan	韩国 - 釜山
katowice	波兰 - 卡托维兹
milan	意大利 - 米兰
portugal	葡萄牙
barcelona	西班牙 - 巴塞罗那
valencia	西班牙 - 瓦伦西亚
malaga	西班牙 - 马拉加
ankara	土耳其 - 安卡拉
izmir	土耳其 - 伊兹密尔
odessa	乌克兰 - 敖德萨
lviv	乌克兰 - 利沃夫
kharkiv	乌克兰 - 哈尔科夫
bulgaria	保加利亚
croatia	克罗地亚
lithuania	立陶宛
suzhou	中国 - 苏州
hangzhou	中国 - 杭州
ningbo	中国 - 宁波
nanjing	中国 - 南京
shenzhen	中国 - 深圳
dongguan	中国 - 东莞
tianjin	中国 - 天津
chongqing	中国 - 重庆
shenyang	中国 - 沈阳
dalian	中国 - 大连
austin	美国 - 奥斯汀
cebu	菲律宾 - 宿务
davao	菲律宾 - 达沃
baguio	菲律宾 - 碧瑶
nanning	中国 - 南宁
canary_islands	加那利群岛
maritius	毛里求斯
reunion	留尼汪
kuwait	科威特
sevastopol	乌克兰 - 塞瓦斯托波尔
riga	拉脱维亚
cuiaba	巴西 - 库亚巴
belohorizonte	巴西 - 贝洛奥里藏特
curitiba	巴西 - 库里提巴
campogrande	巴西 - 大坎普
manaus	巴西 - 马瑙斯
belem	巴西 - 贝伦
salvador	巴西 - 萨尔瓦多
guam	关岛`;

const defaultRegion: SteamDownloadRegion = { value: '', label: '使用 Steam 默认区域' };
const regionCollator = new Intl.Collator('zh-CN');

function countryAndCity(label: string): [string, string] {
  const [country, city = ''] = label.split(' - ', 2);
  return [country, city];
}

export const STEAM_DOWNLOAD_REGIONS: readonly SteamDownloadRegion[] = [
  defaultRegion,
  ...STEAM_DOWNLOAD_REGION_DATA.split('\n')
    .map((line) => {
      const [value, label] = line.split('\t');
      return { value, label };
    })
    .filter(region => region.value !== '')
    .sort((left, right) => {
      const [leftCountry, leftCity] = countryAndCity(left.label);
      const [rightCountry, rightCity] = countryAndCity(right.label);
      return regionCollator.compare(leftCountry, rightCountry) || regionCollator.compare(leftCity, rightCity);
    }),
];
