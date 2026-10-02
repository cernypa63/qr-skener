package cz.pavel.qrskener.scan

import androidx.compose.runtime.snapshots.SnapshotStateList
import androidx.compose.runtime.toMutableStateList
import androidx.lifecycle.ViewModel

class ScanSessionViewModel : ViewModel() {

    val records: SnapshotStateList<ScanRecord> = emptyList<ScanRecord>().toMutableStateList()

    val total: Double
        get() = records.sumOf { it.amount ?: 0.0 }

    fun add(record: ScanRecord) {
        records.add(record)
    }

    fun remove(record: ScanRecord) {
        records.remove(record)
    }

    fun clear() {
        records.clear()
    }

    fun isDuplicate(rawCode: String, now: Long, windowMillis: Long): Boolean {
        val last = records.lastOrNull() ?: return false
        return last.rawCode == rawCode && now - last.timestampMillis < windowMillis
    }
}
