#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_log.h"
#include "esp_adc/adc_oneshot.h"
#include "font5x7.h"

static const char *TAG = "OLED_LAB9_3_CLOSED_LOOP";

// ==============================================================================
// 1. กำหนดขาเชื่อมต่อสำหรับ 7-Pin SPI OLED และ Potentiometer (ADC1)
// ==============================================================================
// OLED 7-Pin (SPI)   ESP32 Pin   Signal Name
// -------------------------------------------------------------
// GND                GND         Ground
// VCC                3.3V        Power (3.3V ห้ามต่อ 5V)
// D0                 GPIO 18     SCK / SCLK (SPI Clock)
// D1                 GPIO 23     MOSI (SPI Master Out Slave In)
// RES                GPIO 4      Hardware Reset (Active LOW)
// DC                 GPIO 2      Data / Command (0=Cmd, 1=Data)
// CS                 GPIO 5      Chip Select (Active LOW)
// -------------------------------------------------------------
// Potentiometer:
// Pin 1 (Left)  -> 3.3V
// Pin 2 (Wiper) -> GPIO 34 (ADC1 Channel 6)
// Pin 3 (Right) -> GND
// ==============================================================================
#define OLED_PIN_SCK    (GPIO_NUM_18) // D0
#define OLED_PIN_MOSI   (GPIO_NUM_23) // D1
#define OLED_PIN_RES    (GPIO_NUM_4)  // RES
#define OLED_PIN_DC     (GPIO_NUM_2)  // DC
#define OLED_PIN_CS     (GPIO_NUM_5)  // CS

#define POT_ADC1_CHAN   (ADC_CHANNEL_6) // GPIO 34

static spi_device_handle_t s_spi_handle = NULL;
static adc_oneshot_unit_handle_t s_adc1_handle = NULL;

// ==============================================================================
// 2. ฟังก์ชันกำหนดค่าเริ่มต้นพิน GPIO และบัสฮาร์ดแวร์ SPI2 (VSPI)
// ==============================================================================
esp_err_t oled_spi_init(void)
{
    gpio_config_t io_conf = {
        .pin_bit_mask = (1ULL << OLED_PIN_DC) | (1ULL << OLED_PIN_RES),
        .mode = GPIO_MODE_OUTPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,
        .pull_down_en = GPIO_PULLDOWN_DISABLE,
        .intr_type = GPIO_INTR_DISABLE,
    };
    gpio_config(&io_conf);

    spi_bus_config_t buscfg = {
        .miso_io_num = -1,               // Write-Only
        .mosi_io_num = OLED_PIN_MOSI,     // GPIO 23
        .sclk_io_num = OLED_PIN_SCK,      // GPIO 18
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
        .max_transfer_sz = 1024 + 16,
    };

    esp_err_t ret = spi_bus_initialize(SPI2_HOST, &buscfg, SPI_DMA_CH_AUTO);
    if (ret != ESP_OK) return ret;

    spi_device_interface_config_t devcfg = {
        .clock_speed_hz = 10 * 1000 * 1000, // 10 MHz
        .mode = 0,                          // Mode 0: CPOL=0, CPHA=0
        .spics_io_num = OLED_PIN_CS,        // GPIO 5
        .queue_size = 7,
    };

    return spi_bus_add_device(SPI2_HOST, &devcfg, &s_spi_handle);
}

// ==============================================================================
// 3. ฟังก์ชันส่งคำสั่งและข้อมูลผ่าน SPI
// ==============================================================================
void oled_send_cmd(uint8_t cmd)
{
    gpio_set_level(OLED_PIN_DC, 0); // DC = 0 (Command)
    spi_transaction_t t;
    memset(&t, 0, sizeof(t));
    t.length = 8;
    t.tx_buffer = &cmd;
    spi_device_polling_transmit(s_spi_handle, &t);
}

void oled_send_data(const uint8_t *data, size_t len)
{
    if (len == 0) return;
    gpio_set_level(OLED_PIN_DC, 1); // DC = 1 (Data)
    spi_transaction_t t;
    memset(&t, 0, sizeof(t));
    t.length = len * 8;
    t.tx_buffer = data;
    spi_device_polling_transmit(s_spi_handle, &t);
}

// ==============================================================================
// 4. Framebuffer ขนาด 1KB และฟังก์ชันวาดกราฟิก
// ==============================================================================
static uint8_t s_oled_buffer[1024];

void oled_clear(void)
{
    memset(s_oled_buffer, 0x00, sizeof(s_oled_buffer));
}

void oled_draw_pixel(int x, int y, bool color)
{
    if (x < 0 || x >= 128 || y < 0 || y >= 64) return;

    int byte_index = x + (y / 8) * 128;
    int bit_offset = y % 8;

    if (color) {
        s_oled_buffer[byte_index] |= (1 << bit_offset);
    } else {
        s_oled_buffer[byte_index] &= ~(1 << bit_offset);
    }
}

void oled_draw_rect(int x, int y, int w, int h, bool color)
{
    for (int i = x; i < x + w; i++) {
        oled_draw_pixel(i, y, color);
        oled_draw_pixel(i, y + h - 1, color);
    }
    for (int j = y; j < y + h; j++) {
        oled_draw_pixel(x, j, color);
        oled_draw_pixel(x + w - 1, j, color);
    }
}

void oled_fill_rect(int x, int y, int w, int h, bool color)
{
    for (int i = x; i < x + w; i++) {
        for (int j = y; j < y + h; j++) {
            oled_draw_pixel(i, j, color);
        }
    }
}

void oled_draw_char(int x, int y, char c, bool color)
{
    if (c < 32 || c > 126) c = '?';
    int font_idx = c - 32;

    for (int col = 0; col < 5; col++) {
        uint8_t line = font5x7[font_idx][col];
        for (int row = 0; row < 7; row++) {
            if (line & (1 << row)) {
                oled_draw_pixel(x + col, y + row, color);
            } else {
                oled_draw_pixel(x + col, y + row, !color);
            }
        }
    }
    for (int row = 0; row < 7; row++) {
        oled_draw_pixel(x + 5, y + row, !color);
    }
}

void oled_draw_string(int x, int y, const char *str, bool color)
{
    while (*str) {
        oled_draw_char(x, y, *str, color);
        x += 6;
        if (x + 6 > 128) break;
        str++;
    }
}

void oled_flush(void)
{
    oled_send_cmd(0x21); // Set Column Address
    oled_send_cmd(0x00);
    oled_send_cmd(0x7F);

    oled_send_cmd(0x22); // Set Page Address
    oled_send_cmd(0x00);
    oled_send_cmd(0x07);

    oled_send_data(s_oled_buffer, sizeof(s_oled_buffer));
}

// ==============================================================================
// 5. การตั้งค่าเริ่มต้นของจอ OLED (Magic Initialization)
// ==============================================================================
void oled_init_display(void)
{
    // Hardware Reset ขา RES (GPIO 4)
    gpio_set_level(OLED_PIN_RES, 0);
    vTaskDelay(pdMS_TO_TICKS(15));
    gpio_set_level(OLED_PIN_RES, 1);
    vTaskDelay(pdMS_TO_TICKS(15));

    // SSD1306 Initialization
    oled_send_cmd(0xAE); // Display OFF
    oled_send_cmd(0x8D); // Charge Pump Setting
    oled_send_cmd(0x14); // Enable Charge Pump 7.5V
    oled_send_cmd(0x20); // Memory Addressing Mode
    oled_send_cmd(0x00); // Horizontal Addressing Mode
    
    // ตั้งค่าทิศทางสแกนให้แถบสีเหลืองอยู่ด้านบน (Lab 9.4 Case 3 Fix)
    oled_send_cmd(0xA1); // Segment Re-map: Column 127 -> SEG0
    oled_send_cmd(0xC8); // COM Output Scan: COM[N-1] -> COM0

    oled_send_cmd(0xAF); // Display ON
}

// ==============================================================================
// 6. กำหนดค่าเริ่มต้น ADC1 สำหรับ Potentiometer (GPIO 34)
// ==============================================================================
void init_potentiometer_adc(void)
{
    adc_oneshot_unit_init_cfg_t init_config1 = {
        .unit_id = ADC_UNIT_1,
        .ulp_mode = ADC_ULP_MODE_DISABLE,
    };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&init_config1, &s_adc1_handle));

    adc_oneshot_chan_cfg_t config = {
        .bitwidth = ADC_BITWIDTH_DEFAULT, // 12-bit (0-4095)
        .atten = ADC_ATTEN_DB_12,         // วัดได้สูงสุด 3.3V
    };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(s_adc1_handle, POT_ADC1_CHAN, &config));
    ESP_LOGI(TAG, "ADC1 Channel 6 (GPIO 34) initialized successfully.");
}

// ==============================================================================
// 7. เรนเดอร์ Multi-Zone UI บนหน้าจอ OLED (Lab 9.3)
// ==============================================================================
void render_multizone_ui(int raw_adc, double percent, const char *footer_msg)
{
    oled_clear();

    // -------------------------------------------------------------
    // Zone 1: Status Bar (แถว 0 - 15: โซนสีเหลือง)
    // -------------------------------------------------------------
    char header_str[24];
    snprintf(header_str, sizeof(header_str), "ESP32 OK RAW:%4d", raw_adc);
    oled_draw_string(2, 4, header_str, true);

    // -------------------------------------------------------------
    // Zone 2: Bar Gauge & Percentage (แถว 16 - 47: โซนสีฟ้า)
    // -------------------------------------------------------------
    // วาดกรอบนอกของ Bar Gauge (พิกัด x:10..118, y:18..28)
    int gauge_x = 8;
    int gauge_y = 18;
    int gauge_w = 112;
    int gauge_h = 10;
    oled_draw_rect(gauge_x, gauge_y, gauge_w, gauge_h, true);

    // คำนวณความกว้างแถบด้านในตามเปอร์เซ็นต์ (0 - 100%)
    int inner_max_w = gauge_w - 4; // 108 pixels
    int fill_w = (int)((percent / 100.0) * inner_max_w);
    if (fill_w > inner_max_w) fill_w = inner_max_w;
    if (fill_w < 0) fill_w = 0;

    if (fill_w > 0) {
        oled_fill_rect(gauge_x + 2, gauge_y + 2, fill_w, gauge_h - 4, true);
    }

    // แสดงตัวเลขเปอร์เซ็นต์และรหัสประจำตัว
    char level_str[24];
    snprintf(level_str, sizeof(level_str), "LEVEL: %5.1f %%", percent);
    oled_draw_string(18, 33, level_str, true);

    // -------------------------------------------------------------
    // Zone 3: Footer Message (แถว 48 - 63: โซนสีฟ้า)
    // -------------------------------------------------------------
    oled_draw_string(4, 50, footer_msg, true);

    // ส่งภาพขึ้นหน้าจอทางฮาร์ดแวร์ SPI (ความเร็ว 10 MHz)
    oled_flush();
}

// ==============================================================================
// 8. ลำดับการทดลองหลัก (app_main)
// ==============================================================================
void app_main(void)
{
    ESP_LOGI(TAG, "Starting Lab 9.3: Closed-Loop IoT Integration (ID: 67030183)...");

    // 1. เริ่มต้น SPI และหน้าจอ OLED
    ESP_ERROR_CHECK(oled_spi_init());
    oled_init_display();

    // 2. เริ่มต้นโมดูล ADC1 (GPIO 34)
    init_potentiometer_adc();

    // ตัวแปรสำหรับ Calibration Model (ตรงกับ Kestrel Server)
    const int raw_min = 150;
    const int raw_max = 3950;
    char oled_msg[24] = "KESTREL: OK 67030183";

    int raw_val = 0;
    int loop_counter = 0;

    while (1) {
        // อ่านค่า ADC1 จากตัวต้านทานปรับค่าได้ (Potentiometer)
        esp_err_t r = adc_oneshot_read(s_adc1_handle, POT_ADC1_CHAN, &raw_val);
        if (r == ESP_OK) {
            // คำนวณสเกลแบบเชิงเส้น (Linear Calibration: 0.0 - 100.0%)
            int clamped = raw_val;
            if (clamped < raw_min) clamped = raw_min;
            if (clamped > raw_max) clamped = raw_max;
            double percent = ((double)(clamped - raw_min) / (raw_max - raw_min)) * 100.0;

            // ส่งข้อมูล Telemetry ออก Serial ทุกรอบ (~50ms)
            // ฟอร์แมตตรงตาม Lab 9.3: "ADC:<raw>\n"
            printf("ADC:%d\n", raw_val);

            // อัปเดตหน้าจอ Multi-Zone UI
            render_multizone_ui(raw_val, percent, oled_msg);
        }

        loop_counter++;
        if (loop_counter % 60 == 0) {
            // สลับข้อความ Footer แสดงรหัสนักศึกษาและสถานะ
            if ((loop_counter / 60) % 2 == 0) {
                snprintf(oled_msg, sizeof(oled_msg), "KESTREL: OK 67030183");
            } else {
                snprintf(oled_msg, sizeof(oled_msg), "STATUS: CALIBRATED");
            }
        }

        vTaskDelay(pdMS_TO_TICKS(50)); // อัตรารีเฟรช 20 Hz (50 ms)
    }
}
