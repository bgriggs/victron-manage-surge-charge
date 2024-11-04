#  Manage Grid Surge Charge with a Victron Inverter
Tool to control Victron inverter mode to avoid grid surge charge hours. For example, during the hours of 4pm - 8pm, the grid charges a higher rate for electricity. This tool will switch the inverter to battery mode during these hours to avoid the surge charge. The inverter will switch back to grid mode after 8pm.

## Requirements
- This uses Venus OS version 3.50 (may work back to 3.20) dbus-flashmq.
https://github.com/victronenergy/dbus-flashmq
- Tested with Multi-Plus II

## Installation
This is a windows service.
