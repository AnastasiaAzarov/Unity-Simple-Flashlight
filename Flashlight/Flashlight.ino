#include <bluefruit.h>

const int led = 13;
const int greenLed = 12;
const int redLed = 11;
const int SW_pin = 9;
const int X_pin = A3;
const int Y_pin = A4;

void setup() {
  Serial.begin(115200);   
  pinMode(led, OUTPUT);
  pinMode(greenLed, OUTPUT);
  pinMode(redLed, OUTPUT);
  pinMode(SW_pin, INPUT_PULLUP);
  Serial.println("Ready");
}

void loop() {
  int xVal = analogRead(X_pin);
  int yVal = analogRead(Y_pin);
  int switchState = digitalRead(SW_pin) == LOW ? 0 : 1;

  // LED follows switch
  digitalWrite(led, switchState == 0 ? HIGH : LOW);
  digitalWrite(greenLed, switchState == 0 ? HIGH : LOW);

  // Pulse red LED 
  digitalWrite(redLed, HIGH);
  delay(5);
  digitalWrite(redLed, LOW);

  Serial.print(xVal);
  Serial.print(" ");
  Serial.print(yVal);
  Serial.print(" ");
  Serial.println(switchState); 

  delay(25);
}
