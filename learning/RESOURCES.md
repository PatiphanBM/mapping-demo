# Queue, Worker & Kafka Resources

## Knowledge

- [Apache Kafka — Introduction](https://kafka.apache.org/intro)
  เอกสารทางการ สั้นที่สุดที่อธิบาย event, producer, consumer, topic, partition และ retention ใช้สำหรับ: นิยามศัพท์ Kafka ทุกคำ
- [Apache Kafka — Documentation: Design](https://kafka.apache.org/documentation/#design)
  อธิบายกลไกภายใน (log, offset, consumer position, delivery semantics) ใช้สำหรับ: บทเรื่อง offset/commit และ at-least-once
- [Confluent Developer — Apache Kafka 101 (คอร์สวิดีโอฟรี)](https://developer.confluent.io/courses/apache-kafka/events/)
  วิดีโอสั้น 3–9 นาทีต่อหัวข้อ topics, partitions, producers, consumers ใช้สำหรับ: ดูภาพเคลื่อนไหวประกอบเมื่ออ่านแล้วยังไม่เห็นภาพ
- [AWS — Message Queues](https://aws.amazon.com/message-queue/)
  นิยาม message queue, producer/consumer และประโยชน์ (decouple, buffer, smooth spiky workloads) ใช้สำหรับ: แนวคิด queue ทั่วไปก่อนเข้า Kafka
- [Microsoft Learn — Worker Services in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
  นิยาม BackgroundService / IHostedService และ `ExecuteAsync` ใช้สำหรับ: อ่านโค้ดฝั่ง `MappingDemo.Worker`
- [Chris Richardson (microservices.io) — Pattern: Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html)
  ต้นฉบับของ pattern ที่ `OutboxWriter` / `OutboxDispatcher` ใช้ รวมข้อเสีย (ส่งซ้ำได้ → consumer ต้อง idempotent) ใช้สำหรับ: บทเรื่อง outbox
- [Confluent — .NET Client for Apache Kafka](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
  เอกสารของ Confluent.Kafka ที่โปรเจกต์ใช้ ใช้สำหรับ: `ProducerConfig`, `ConsumerConfig`, `Commit`, `Pause`
- [Microsoft — Queue-Based Load Leveling pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/queue-based-load-leveling)
  concept หลักของ queue ในหน้าเดียว: buffer, decoupling, at-least-once → idempotent, dead-letter, ลำดับ message ใช้สำหรับ: แหล่งหลักบทที่ 1 และเรื่อง failure
- [Enterprise Integration Patterns (Hohpe & Woolf, 2003) — Messaging patterns](https://www.enterpriseintegrationpatterns.com/patterns/messaging/)
  ต้นฉบับชื่อ pattern: Point-to-Point, Publish-Subscribe, Competing Consumers, Dead Letter Channel ใช้สำหรับ: นิยาม concept
- [IBM — MQSeries: An Introduction to Messaging and Queuing (1993)](https://public.dhe.ibm.com/software/mqseries/pdf/horaa101.pdf)
  เอกสารต้นทางของแนวคิด "Messaging and Queuing" ใช้สำหรับ: ประวัติและนิยาม decoupling
- [Kreps, Narkhede & Rao — Kafka: a Distributed Messaging System for Log Processing (NetDB 2011)](https://people.csail.mit.edu/matei/courses/2015/6.S897/readings/kafka.pdf)
  เปเปอร์ต้นฉบับของ Kafka: ทำไมคิวองค์กรเดิมไม่เหมาะ, consumer เก็บตำแหน่งเอง, retention ตามเวลา, pull model ใช้สำหรับ: Kafka ต่างจาก queue เดิมอย่างไร
- [MacTutor — A. K. Erlang](https://mathshistory.st-andrews.ac.uk/Biographies/Erlang/)
  ชีวประวัติจาก St Andrews, ที่มาของ queueing theory (1909) ใช้สำหรับ: ประวัติ
- [GitHub Blog — Introducing Resque (2009)](https://github.blog/news-insights/the-library/introducing-resque/)
  ตัวอย่างต้นแบบ background job + worker ในเว็บแอป ใช้สำหรับ: ประวัติ queue + worker
- [AWS News Blog — Amazon SQS: 15 Years and Still Queueing](https://aws.amazon.com/blogs/aws/amazon-sqs-15-years-and-still-queueing/)
  ประวัติ SQS (เบต้า 2004, production 2006) ใช้สำหรับ: ประวัติ
- [Microsoft — Asynchronous Request-Reply pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/asynchronous-request-reply)
  202 Accepted + status endpoint + polling และ field ที่ status ควรมี ใช้สำหรับ: บทที่ 3 (สถานะงาน)
- [RFC 1945 (HTTP/1.0, 1996)](https://www.rfc-editor.org/rfc/rfc1945.html) และ [RFC 2068 (HTTP/1.1, 1997)](https://www.rfc-editor.org/rfc/rfc2068)
  นิยามต้นฉบับของ 202 Accepted ("intentionally non-committal") ใช้สำหรับ: ประวัติของการตอบรับงานแบบ async
- [Apache Kafka — Message Delivery Semantics](https://kafka.apache.org/documentation/#semantics) (ต้นฉบับ markdown: [apache/kafka docs/design/design.md](https://github.com/apache/kafka/blob/trunk/docs/design/design.md))
  นิยาม at-most/at-least/exactly once และผลของการ commit ก่อน/หลังทำงาน ใช้สำหรับ: บทที่ 4
- [EIP — Idempotent Receiver](https://www.enterpriseintegrationpatterns.com/patterns/messaging/IdempotentReceiver.html)
  ต้นฉบับ pattern ฝั่งรับต้องรับ message ซ้ำได้ ใช้สำหรับ: บทที่ 4
- [Marc Brooker — Exponential Backoff And Jitter (AWS Architecture Blog, 2015)](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
  ทำไม retry ต้องเว้นระยะและสุ่ม ใช้สำหรับ: retry
- [Wikipedia — Fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing)
  ความเชื่อผิด 8 ข้อ (Deutsch 1994) ใช้สำหรับ: ที่มาของการออกแบบรองรับความล้มเหลว
- [Apache Kafka — KafkaConsumer javadoc (Consumer Groups and Topic Subscriptions, Multi-threaded Processing)](https://kafka.apache.org/39/javadoc/org/apache/kafka/clients/consumer/KafkaConsumer.html)
  partition หนึ่งตัวต่อ consumer หนึ่งตัวใน group, rebalance เมื่อเพิ่ม process, "total threads … limited by the total number of partitions" ใช้สำหรับ: บทที่ 5
- [Confluent Developer — Kafka 101: Consumers](https://developer.confluent.io/courses/apache-kafka/consumers/)
  "no two consumers in the same group read from the same partition", rebalance เมื่อเพิ่ม/ดับ ใช้สำหรับ: บทที่ 5
- [Confluent Docs — Kafka Consumer](https://docs.confluent.io/platform/current/clients/consumer.html)
  consumer group, heartbeat/session timeout แล้ว rebalance ใช้สำหรับ: บทที่ 5 (rebalance)
- หมายเหตุ: ยังไม่พบเอกสารทางการที่เขียนตรง ๆ ว่า "consumer เกินจำนวน partition จะว่าง" บทที่ 5 อนุมานจากสองข้อความด้านบน
- [microservices.io — Pattern: Polling publisher](https://microservices.io/patterns/data/polling-publisher.html) และ [Transaction log tailing](https://microservices.io/patterns/data/transaction-log-tailing.html)
  สองวิธีทำ message relay ข้อเสียของ polling คือส่งตามลำดับได้ยาก ใช้สำหรับ: บทที่ 6

## Wisdom (Communities)

- [Confluent Community Forum](https://forum.confluent.io/)
  ฟอรั่ม Discourse ที่ active มีหมวด Clients และ Architecture and Design ใช้สำหรับ: ถามคำถามออกแบบ เช่น outbox, consumer group, การจัดการ retry
- [Stack Overflow — แท็ก apache-kafka](https://stackoverflow.com/questions/tagged/apache-kafka)
  ใช้สำหรับ: error เฉพาะของ client library เช่น Confluent.Kafka

## Gaps
- ยังไม่มีแหล่งภาษาไทยที่น่าเชื่อถือพอ
