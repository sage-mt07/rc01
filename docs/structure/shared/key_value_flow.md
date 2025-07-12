# Key-Value Flow Architecture (POCO ↔ Kafka)

## 1. 概要

本資料は、Query namespace に定義された POCO および LINQ式から Kafka へ送信するフロー（Produce）と、Kafka から受信して POCO に復元するフロー（Consume）を一貫して設計するための責務分解図である。
今回よりPOCO-Query Mapping Layerを新設し、POCOモデルとKSQLクエリ・Key/Valueペアの対応管理を独立レイヤーとして設計する。

---

## 2. 全体構造図（双方向）

```
[Query]
   ⇅
[POCO-Query Mapping]
   ⇅
[KsqlContext]
   ⇅
[Messaging]
   ⇅
[Serialization]
   ⇅
[Kafka]
```



## 3. Produce Flow（POCO → Kafka）
```
[Query/EntitySet<T>]
↓ LINQ式, POCO
[POCO-Query Mapping/MappingManager]
↓ POCO, KSQL, Key/Value対応表
[KsqlContext/ExtractKeyValue()]
↓ T → key, value
[Messaging/IKafkaProducer<T>.Produce()]
↓ key, value
[Serialization/AvroSerializer]
↓ byte[]
[Kafka]
→ Topic送信
```

### 🧱 責務一覧

| レイヤー     | クラス名             | 主な責務                                  |
|--------------|----------------------|-------------------------------------------|
| Query        | EntitySet<T>         | LINQ式とPOCOを提供                         |
| POCO-Query Mapping |	MappingManager|	POCOとKSQL・Key/Valueのマッピング管理 |
| KsqlContext  | ExtractKeyValue()    | マッピング情報をもとにフロー統括・初期化             |
| Messaging    | IKafkaProducer<T>    | メッセージ送信、トピック指定              |
| Serialization| AvroSerializer       | key/value の Avro変換（Confluent）        |
| Kafka        | Kafka Broker         | メッセージ配信                            |

---

## 4. Consume Flow（Kafka → POCO）
```
[Kafka]
↓ メッセージ受信
[Serialization/AvroDeserializer]
↓ key, value（byte[] → object）
[Messaging/IKafkaConsumer<TKey, TValue>]
↓ key, value
[KsqlContext]
↓
[POCO-Query Mapping/MappingManager]
↓
[Query/EntitySet<T>]
→ POCO再構成・アプリロジックへ
```


### 🧱 責務一覧

| レイヤー     | クラス名               | 主な責務                                     |
|--------------|------------------------|----------------------------------------------|
|Kafka	|Kafka Broker	|メッセージ受信|
|Serialization	|AvroDeserializer	|Avro → key/value 変換（Confluent）|
|Messaging	|IKafkaConsumer<TKey, TValue>	|メッセージ処理、key/value復元|
|KsqlContext	||	統括・DI管理|
|POCO-Query |Mapping	MappingManager	|key/value から POCO復元マッピング|
|Query	|EntitySet<T>	|POCO再構成・LINQ式等の提供|

---

## 5. 注意点

- Key定義はLINQ式/Queryベースで統一（POCO属性依存を排除）
- POCO-Query Mapping Layerが対応管理責務を一元化
- key/valueのAvro変換はConfluent公式に完全依存
- IKafkaConsumer は再生成されたTKey/TValueの型安全性を保持
- 各構成はDIにより初期化、KsqlContextが統括

