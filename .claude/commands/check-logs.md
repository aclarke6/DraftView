# check-logs

Check production logs for a systemd service on this server.

## Usage

```
/check-logs [service] [options]
```

If no service is specified, default to `draftview`.

## What to do

1. If a service name is given as an argument, use it. Otherwise use `draftview`.

2. Run `sudo systemctl status <service> --no-pager` and summarise the current state — is it active, failed, restarting? When did it last start?

3. Run `sudo journalctl -u <service> -n 100 --no-pager` and report:
   - Any ERROR or CRITICAL entries, with timestamps
   - Any WARN entries that appear more than once
   - The last 5 lines, verbatim, so the most recent activity is visible
   - Whether the service appears healthy or not, with a one-line verdict

4. If the word "errors" or "error" appears in the arguments, filter to `journalctl -u <service> -p err -n 50 --no-pager` instead and list every entry.

5. If the word "nginx" appears in the arguments, also check `/var/log/nginx/error.log` (last 50 lines) and report any 5xx errors or upstream failures.

6. If the word "follow" or "live" appears in the arguments, run `sudo journalctl -u <service> -f --no-pager` to stream in real time.

7. After reporting, suggest the most likely next diagnostic step if anything looks wrong.
