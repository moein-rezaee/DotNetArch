# kit ‏{{Area}} - قراردادها

## سطح عمومی
{{ContractSummary}}

## ثبت
برای هر provider یکبار `Add<Provider>{{Area}}Provider()` و سپس یکبار `Add{{Area}}Kit(IConfiguration)`. آخرین ثبتِ یک نام provider برنده است.

## کلیدهای پیکربندی
غیرحساس:

| کلید | معنی |
| --- | --- |
| `{{Area}}:Provider` | نام provider؛ با چند provider ثبت‌شده الزامی است |
{{ConfigTable}}
secretها (کلیدهای UPPER_CASE محیط / secret store):

| کلید | استفاده |
| --- | --- |
{{SecretTable}}
## خطاها
`{{Area}}Exception` دارای `ErrorCode` پایدار (پیش‌فرض `{{AreaSnake}}_error`) است. مشکل options با `InvalidOperationException` و نام کلید گزارش می‌شود.
