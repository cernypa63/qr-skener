package cz.pavel.vyska

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class NmeaTest {

    @Test
    fun parsesAltitudeAndSatellites() {
        val fix = Nmea.parseGga("\$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47")!!
        assertEquals(545.4, fix.altitudeMsl, 0.001)
        assertEquals(8, fix.satellites)
    }

    @Test
    fun acceptsOtherTalkers() {
        assertEquals(312.0, Nmea.parseGga("\$GNGGA,101010.00,5005.0,N,01425.0,E,2,12,0.7,312.0,M,44.0,M,,*00")!!.altitudeMsl, 0.001)
    }

    @Test
    fun rejectsInvalidFixAndOtherSentences() {
        assertNull(Nmea.parseGga("\$GPGGA,123519,,,,,0,00,,,M,,M,,*66"))
        assertNull(Nmea.parseGga("\$GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A"))
        assertNull(Nmea.parseGga("nesmysl"))
    }
}
