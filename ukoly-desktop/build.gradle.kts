import org.jetbrains.compose.desktop.application.dsl.TargetFormat

plugins {
    kotlin("jvm") version "1.9.23"
    id("org.jetbrains.compose") version "1.6.2"
}

group = "cz.pavel.ukoly"
version = "1.0.0"

kotlin {
    jvmToolchain(17)
}

dependencies {
    implementation(compose.desktop.currentOs)
    implementation(compose.material3)
    implementation(compose.materialIconsExtended)
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-swing:1.8.0")
    implementation("commons-net:commons-net:3.10.0")
    implementation("org.json:json:20240303")

    testImplementation("junit:junit:4.13.2")
}

compose.desktop {
    application {
        mainClass = "cz.pavel.ukoly.MainKt"

        nativeDistributions {
            targetFormats(TargetFormat.Msi, TargetFormat.Exe)
            packageName = "Ukoly"
            packageVersion = "1.0.0"
            description = "Úkoly – sdílený seznam úkolů v JSON souboru na FTP"
            vendor = "Pavel Černý"
            modules("java.naming", "jdk.crypto.ec")

            windows {
                menuGroup = "Úkoly"
                shortcut = true
                dirChooser = true
                perUserInstall = true
                upgradeUuid = "6f3d2a8e-4b1c-4e7a-9d55-2c8f0b7e1a43"
                iconFile.set(project.file("src/main/resources/ukoly.ico"))
            }
        }
    }
}
