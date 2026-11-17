# AreaId (พื้นที่ จังหวัด/อำเภอ/ตำบล): สิ่งที่พบ และเรื่องที่ทีมต้องตัดสินใจ

> ข้อมูล ณ 17 พ.ย. 2026 ใช้ประกอบการคุยกับทีม ยังไม่มีการแก้โค้ดหรือข้อมูลจากเอกสารนี้

## 1. สรุปสั้น

- API ใหม่ **บันทึก `areaId` ไม่ได้เลย** เพราะตาราง `TblArea` ใน GCC1111DB ว่าง (0 แถว) `AddNewAccount` และ `AddNewService` จึงตอบ 400 ทุกค่าที่ส่งมา
- ระบบ GCC เดิมเก็บพื้นที่ในตาราง `tbl_area` ของ MySQL `db_gcc_new` แต่ข้อมูลชุดนี้ยังไม่ได้ย้ายมา
- ระบบเดิมแทบไม่ได้ใช้พื้นที่ของ **เรื่อง**: ทั้งตาราง `tbl_service` มีแค่ 1 เรื่องที่ใส่ `Area_ID` (ปี 2557)

## 2. ตัวอย่างที่ไล่มา

AreaId `980459C35241E273D6AB6550DD47D4D0` (ค่าเดียวที่มีใน `db_gcc_new.tbl_service.Area_ID`)

| ชั้น | `Area_Type` | `Name_TH` | `Zipcode` |
|---|---|---|---|
| ตัว id นี้ | `SUBDISTRICT` | เขาพระ | NULL |
| แม่ | `DISTRICT` | เมือง | 26000 |
| บนสุด | `PROVINCE` | นครนายก | |

คือ **ตำบลเขาพระ อำเภอเมืองนครนายก จังหวัดนครนายก 26000**

เรื่องที่ใช้: `Code 57020600020` "สอบถาม การตั้งผู้จัดการมรดก" เปิดเมื่อ 2014-02-06 13:23:19, `Service_Area` เป็น NULL

## 3. ระบบเดิม: `db_gcc_new.tbl_area` (MySQL)

อ้างอิงโค้ด: `2022GCC_Production/ClassCode/Repository/AreaRepo.cs`, `ClassCode/TOTCMModel.edmx`

| คอลัมน์ | ความหมาย |
|---|---|
| `ID` | varchar(32) |
| `Ref_ID` | ชั้นบน (จังหวัดชี้ที่ตัวเอง) |
| `Name_TH` / `Name_EN` | ชื่อ |
| `Area_Type` | `PROVINCE` / `DISTRICT` / `SUBDISTRICT` |
| `Zipcode` | มีค่าที่ชั้นอำเภอ |
| `Remark`, `Is_Default`, `Is_Enable`, `Created…`, `Updated…` | |

- เป็นข้อมูล 3 ชั้น: จังหวัด → อำเภอ → ตำบล เชื่อมกันด้วย `Ref_ID`
- ข้อมูลจริงใช้ `Area_Type` เป็นคำเต็ม แม้ `AreaRepo.Add()` จะเขียนเป็น `P` / `D` / `S`
- ระบบเดิมแสดงพื้นที่ด้วย `AreaRepo.GetAreaText()` ซึ่งไล่ `Ref_ID` ขึ้นไปแล้วต่อชื่อ จังหวัด อำเภอ ตำบล

## 4. GCC1111DB (DB ของ API ใหม่)

| ตาราง / คอลัมน์ | สถานะ |
|---|---|
| `TblArea` | **0 แถว** |
| `TblService.Area_ID` | ว่างทั้ง 184 แถว |
| `TblAccount.AreaId` | มีค่า 61 จาก 19,969 แถว: 59 แถวเป็น id แบบ `tbl_area` เดิม (43 ค่าไม่ซ้ำ) และ 2 แถวเป็นข้อความ " ราขเทวี" ทุกค่าชี้ไปไหนไม่ได้ เพราะ `TblArea` ว่าง |
| `TblProvince` / `TblDistrict` / `TblSubDistrict` | มีข้อมูลและมี endpoint แล้ว: `GetProvince`, `GetDistrict`, `GetSubdistrict`, `GetAddressDataset` |

- **การตรวจของ API:** `AddNewAccount` ([AccountController.cs](../Controllers/AccountController.cs)) และ `AddNewService` ([ServicesController.cs](../Controllers/ServicesController.cs)) ตรวจ `areaId` กับ `TblArea`
- **เรื่องนี้บันทึกไว้แล้ว:** [HANDOFF.md](../HANDOFF.md) ข้อ "บันทึก 'พื้นที่' ไม่ได้" และ "ต้องตัดสินใจ" ข้อ 1

## 5. คำถามถึงทีม

1. **เรื่อง (`AddNewService`) ยังต้องเก็บพื้นที่ไหม**
   ระบบเดิมใช้ครั้งเดียวในปี 2557 ถ้าไม่ต้อง ฝั่ง frontend ก็ไม่ต้องส่ง `areaId` และไม่ต้องทำ drop-down พื้นที่ในหน้าเปิดเรื่อง
2. **ผู้ติดต่อ (`AddNewAccount`) ต้องเก็บจังหวัด/อำเภอ/ตำบลไหม** ถ้าต้อง จะใช้แบบไหน

   | | ก. ย้าย `tbl_area` จากระบบเดิมมาใส่ `TblArea` | ข. ใช้ `TblSubDistrict` ที่มีอยู่ |
   |---|---|---|
   | id เดิมใน `TblAccount` (43 ค่า) | ใช้ได้ทันที | ต้อง map ด้วยชื่อ หรือปล่อยไว้ |
   | endpoint รายการพื้นที่ | ต้องทำเพิ่มจาก `TblArea` | มีแล้ว |
   | งานฝั่ง API | ย้ายข้อมูลเข้า DB | เปลี่ยน validation ของ `areaId` ไปตรวจกับ `TblSubDistrict` |
   | หมายเหตุ | ต้องมีคนย้ายข้อมูลจาก MySQL | HANDOFF.md เสนอแบบนี้ไว้ |

3. **ระบบ GCC เดิมยังอ่าน/เขียน `AreaId` ใน GCC1111DB อยู่ไหม** ถ้ายังใช้อยู่ ต้องเก็บ id แบบ `tbl_area` (ข้อ ก) ไม่งั้นระบบเดิมจะอ่านค่าที่ API ใหม่บันทึกไม่ออก

## 6. ภาคผนวก: SQL ที่ใช้ (รันใน MySQL `db_gcc_new` ของระบบเดิม)

```sql
-- AreaId นี้คือพื้นที่อะไร ไล่ขึ้นไปถึงจังหวัด (MySQL 5.x ไม่มี recursive CTE จึง join 3 ชั้น)
SELECT a.ID, a.Area_Type, a.Name_TH, a.Zipcode, a.Is_Enable,
       p1.Name_TH AS parent_name, p1.Area_Type AS parent_type, p1.Zipcode AS parent_zip,
       p2.Name_TH AS top_name,    p2.Area_Type AS top_type
FROM `db_gcc_new`.`tbl_area` a
LEFT JOIN `db_gcc_new`.`tbl_area` p1 ON p1.ID = a.Ref_ID  AND a.Ref_ID  <> a.ID
LEFT JOIN `db_gcc_new`.`tbl_area` p2 ON p2.ID = p1.Ref_ID AND p1.Ref_ID <> p1.ID
WHERE a.ID = '980459C35241E273D6AB6550DD47D4D0';

-- ค่า Area_ID ทั้งหมดที่เรื่องใช้
SELECT Area_ID, COUNT(*) AS Total
FROM `db_gcc_new`.`tbl_service`
WHERE Area_ID IS NOT NULL AND TRIM(Area_ID) <> ''
GROUP BY Area_ID;

-- เรื่องที่ใช้ AreaId นี้
SELECT ID, Code, Summary, Service_Area, Date_Opened, Created
FROM `db_gcc_new`.`tbl_service`
WHERE Area_ID = '980459C35241E273D6AB6550DD47D4D0';
```
