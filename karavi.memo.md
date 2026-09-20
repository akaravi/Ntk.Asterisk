manager_custom.conf

amintkuser
AsWsd@345685


[general]
enabled = yes
bindaddr = 0.0.0.0
port = 5038

[amintkuser]
secret = AsWsd@345685
deny = 0.0.0.0/0					;karavi 14040517
permit = 127.0.0.1/255.255.255.0	;karavi 14040517
permit = 192.168.0.0/255.255.0.0	;karavi 14040517
permit = 78.157.42.0/255.255.0.0
permit = 95.38.10.25/255.255.255.0
permit = 95.38.10.178/255.255.255.0
read = all							;karavi 14040517
write = all 						;karavi 14040517




windows :
New-NetFirewallRule -DisplayName "karvi Asterisk FastAGI" -Direction Inbound -LocalPort 4573 -Protocol TCP -Action Allow

extensions_custom.conf

[custom-smartroute] 
exten => s,1,NoOp(=== Executing Smart Call Routing for ${CALLERID(num)} ===)
same => n,AGI(agi://<IP_سیستم_ویندوز_شما>:4573/smartroute)
same => n,Hangup()

[custom-smartroute] ;karavi 14050629
exten => s,1,NoOp(=== Executing Smart Call Routing for ${CALLERID(num)} ===)
same => n,AGI(agi://h.ntk.ir:4573/smartroute)
same => n,Hangup()