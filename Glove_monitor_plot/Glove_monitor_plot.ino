#include <Wire.h>
#include "MPU6050.h"

#define NUM_IMU 5
#define TCA_ADDR 0x70

MPU6050 imu[NUM_IMU];

// Conversion factors
#define ACCEL_SCALE 16384.0  // LSB/g @ ±2g
#define GYRO_SCALE 131.0     // LSB/(°/s) @ ±250 dps

// Calibration offsets
float accelOffset[NUM_IMU][3] = {0};
float gyroOffset[NUM_IMU][3]  = {0};

void tcaSelect(uint8_t channel) {
  if(channel > 7) return;
  Wire.beginTransmission(TCA_ADDR);
  Wire.write(1 << channel);
  Wire.endTransmission();
}

void calibrateIMU(int imuIndex) {
  long ax_sum = 0, ay_sum = 0, az_sum = 0;
  long gx_sum = 0, gy_sum = 0, gz_sum = 0;
  const int samples = 500;

  for (int i = 0; i < samples; i++) {
    int16_t ax, ay, az, gx, gy, gz;
    imu[imuIndex].getMotion6(&ax, &ay, &az, &gx, &gy, &gz);
    ax_sum += ax;
    ay_sum += ay;
    az_sum += az;
    gx_sum += gx;
    gy_sum += gy;
    gz_sum += gz;
    delay(2);
  }

  accelOffset[imuIndex][0] = (float)ax_sum / samples;
  accelOffset[imuIndex][1] = (float)ay_sum / samples;
  accelOffset[imuIndex][2] = (float)az_sum / samples - ACCEL_SCALE; // subtract 1g
  gyroOffset[imuIndex][0]  = (float)gx_sum / samples;
  gyroOffset[imuIndex][1]  = (float)gy_sum / samples;
  gyroOffset[imuIndex][2]  = (float)gz_sum / samples;
}

void setup() {
  Serial.begin(115200);
  Wire.begin();

  for (int i = 0; i < NUM_IMU; i++) {
    tcaSelect(i);
    imu[i].initialize();
    if (imu[i].testConnection()) {
      Serial.print("IMU "); Serial.print(i); Serial.println(" connected");
      Serial.print("Calibrating IMU "); Serial.println(i);
      calibrateIMU(i);
      Serial.println("Done!");
    } else {
      Serial.print("IMU "); Serial.print(i); Serial.println(" failed");
    }
    delay(200);
  }
}

void loop() {
  Serial.println("IMU Data (Accel in g, Gyro in °/s):");
  Serial.println("IMU\tax[g]\tay[g]\taz[g]\tgx[dps]\tgy[dps]\tgz[dps]");

  for (int i = 0; i < NUM_IMU; i++) {
    tcaSelect(i);
    int16_t ax, ay, az, gx, gy, gz;
    imu[i].getMotion6(&ax, &ay, &az, &gx, &gy, &gz);

    float ax_g = (ax - accelOffset[i][0]) / ACCEL_SCALE;
    float ay_g = (ay - accelOffset[i][1]) / ACCEL_SCALE;
    float az_g = (az - accelOffset[i][2]) / ACCEL_SCALE;
    float gx_dps = (gx - gyroOffset[i][0]) / GYRO_SCALE;
    float gy_dps = (gy - gyroOffset[i][1]) / GYRO_SCALE;
    float gz_dps = (gz - gyroOffset[i][2]) / GYRO_SCALE;

    Serial.print("IMU"); Serial.print(i); Serial.print("\t");
    Serial.print(ax_g, 3); Serial.print("\t");
    Serial.print(ay_g, 3); Serial.print("\t");
    Serial.print(az_g, 3); Serial.print("\t");
    Serial.print(gx_dps, 3); Serial.print("\t");
    Serial.print(gy_dps, 3); Serial.print("\t");
    Serial.println(gz_dps, 3);
  }

  Serial.println();
  delay(50); // ~20 Hz
}
