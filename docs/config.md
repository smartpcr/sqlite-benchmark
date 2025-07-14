## ⚙️ Configuration Recommendations by Payload Size:

### Small Payloads (< 256KB) - OPTIMAL:
```
sqlPRAGMA page_size = 4096;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = 2000;
```
### Medium Payloads (256KB - 1MB) - CONDITIONAL:
```
sqlPRAGMA page_size = 8192;  -- Larger pages for bigger data
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = 10000;
PRAGMA mmap_size = 30000000000;  -- Memory mapping helps
```
### Large Payloads (> 1MB) - AVOID:

Store files externally, keep metadata in SQLite
Use data sharding principles and separate DB files for large BLOB tables performance - SQLite: Optimal PAGE_SIZE - Stack Overflow
