# LoxNet Zigbee2MQTT Bridge

A lightweight service that bridges Zigbee2MQTT devices with Loxone LightControllerV2 subcontrols using the LoxNet client library. It keeps dimmer and ColorPickerV2 subcontrols in sync without implementing moods or modifying Loxone configuration.

## Running with Docker Compose

1. Build and start the stack:
   ```sh
   docker compose -f docker-compose.example.yml up --build
   ```
2. Place your configuration file at `./data/config.yaml` or set `CONFIG` to another path.
3. Browse `http://localhost:8080/health` to confirm connectivity and mapping status.

## Discovering Loxone subcontrols

1. Run the bridge.
2. Call `GET /discover/loxone/subcontrols` on port `8080`.
3. Locate subcontrols of type `Dimmer` or `ColorPickerV2` and copy their `uuidAction` values for your mappings.

## Mapping Zigbee2MQTT devices

* Use device topics in the form `zigbee2mqtt/<device>`.
* Commands are published to `<device>/set` automatically.
* The bridge publishes minimal MQTT payloads so devices only receive fields that change.

## Troubleshooting

* `GET /health` returns overall status plus per-mapping errors such as missing subcontrols or type mismatches.
* Ensure `loxoneUuidAction` and `kind` match the actual subcontrol type in the Loxone structure file.
* Verify MQTT credentials and connectivity if `/health` reports `mqtt: disconnected`.
