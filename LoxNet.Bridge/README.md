# LoxNet Zigbee2MQTT Bridge

A lightweight service that bridges Zigbee2MQTT devices with Loxone LightControllerV2 subcontrols using the LoxNet client library. It keeps dimmer and ColorPickerV2 subcontrols in sync without implementing moods or modifying Loxone configuration.

## Running with Docker Compose

1. Build and start the stack:
   ```sh
   docker compose -f docker-compose.example.yml up --build
   ```
2. Copy `./data/config.example.yaml` to `./data/config.yaml` and update the credentials, or set `CONFIG` to another path.
3. Browse `http://localhost:8080/health` to confirm connectivity and mapping status.

## Discovering Loxone subcontrols

1. Run the bridge.
2. Call `GET /discover/loxone/subcontrols` on port `8080`.
3. Locate subcontrols of type `Dimmer` or `ColorPickerV2` and copy their `uuidAction` values for your mappings.

## Mapping Zigbee2MQTT devices

* Use device topics in the form `zigbee2mqtt/<device>`.
* Commands are published to `<device>/set` automatically.
* The bridge publishes minimal MQTT payloads so devices only receive fields that change.

## Configuration examples

### Dimmer mapping

```yaml
loxone:
  host: "192.168.1.10"
  user: "bridge"
  password: "secret"

mqtt:
  host: "mqtt"

mappings:
  - name: "Kitchen spots"
    kind: "dimmer"
    mqttTopic: "zigbee2mqtt/kitchen_spots"
    loxoneUuidAction: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
    options:
      preferMqttState: true
```

### ColorPickerV2 mapping

```yaml
loxone:
  host: "192.168.1.10"
  user: "bridge"
  password: "secret"

mqtt:
  host: "mqtt"

mappings:
  - name: "Kitchen strip"
    kind: "colorpickerv2"
    mqttTopic: "zigbee2mqtt/kitchen_strip"
    loxoneUuidAction: "ffffffff-1111-2222-3333-444444444444"
    options:
      allowColor: true
      preferMqttState: true
```

### MQTT payloads sent by the bridge

```json
{ "state": "ON" }
```

```json
{ "brightness": 127 }
```

```json
{ "color_temp": 250 }
```

```json
{ "color": { "h": 120, "s": 100, "v": 75 } }
```

## Troubleshooting

* `GET /health` returns overall status plus per-mapping errors such as missing subcontrols or type mismatches.
* Ensure `loxoneUuidAction` and `kind` match the actual subcontrol type in the Loxone structure file.
* Verify MQTT credentials and connectivity if `/health` reports `mqtt: disconnected`.
