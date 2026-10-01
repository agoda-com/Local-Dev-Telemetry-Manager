| Concern | JSON column | Child table |
| --- | --- | --- |
| Query one timer | Yes cuz JSON is easy to scan through and storage-efficient but as the json getting larger, it becomes slower | Relation database but storage heavy |
| Aggregate timer durations | uses a little slower searches | uses B-tree for more efficient searches |
| Dynamic timer names | Not dedicated timer names | It can have child databases with specific names |
| Indexing | can index in postgresql | yes the same as json column |
| Storage/row count | More row count less storage | less row count but more storage |
| Query complexity | GIN / Expression | B-tree |
