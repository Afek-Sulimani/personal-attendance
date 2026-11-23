import { test, expect } from "@playwright/test";
import { mkdir, writeFile } from "node:fs/promises";
import { DemoWorkspace } from "./DemoWorkspace";
import { AttendancePage } from "../pages/AttendancePage";
import { CorrectionReviewPage } from "../pages/CorrectionReviewPage";
import { latestPastWorkday } from "../support/workday";

test("Record a synthetic company attendance and manager approval tour", async({browser,baseURL})=>{
 const workspace=await DemoWorkspace.create(baseURL!);
 let recordingContext;
 try {
  await workspace.invite("PayrollRepresentative"); await workspace.invite("HR");
  await workspace.invite("TeamLead"); await workspace.invite("Employee","TeamLead");
  await workspace.configureCompany();
  const state=await workspace.snapshot();
  const setup=await browser.newContext({baseURL,storageState:state.sessions.Employee});
  const setupPage=await setup.newPage();
  const attendance=new AttendancePage(setupPage);
  for(const offset of [4,3,2]) {await attendance.open();await attendance.correctMissingDay(latestPastWorkday(offset).date);}
  await setup.clearCookies();await setup.addCookies(state.sessions.TeamLead.cookies);
  const review=new CorrectionReviewPage(setupPage);await review.open();
  for(const offset of [4,3,2]) await review.approve("Lucas Silva",latestPastWorkday(offset).date);
  await setup.close();
  await mkdir("test-results/demo-media",{recursive:true});
  recordingContext=await browser.newContext({baseURL,storageState:state.sessions.Employee,viewport:{width:1440,height:960},recordVideo:{dir:"test-results/demo-media",size:{width:1440,height:960}}});
  const page=await recordingContext.newPage(); const video=page.video()!;
  async function chapter(text:string){
   await page.evaluate(text=>{document.querySelector("#demo-caption")?.remove();const caption=document.createElement("div");caption.id="demo-caption";caption.textContent=`FICTIONAL DATA · ${text}`;caption.style.cssText="position:fixed;bottom:18px;left:50%;transform:translateX(-50%);z-index:99999;background:#103d4c;color:white;padding:14px 28px;border-radius:16px;font:600 18px system-ui;box-shadow:0 6px 28px #0003;white-space:nowrap";document.body.append(caption);},text);
   await page.waitForTimeout(3000);
  }
  const employee=new AttendancePage(page);await employee.open();
  await chapter("Lucas Silva · Employee workspace");
  await employee.clockIn();await chapter("Clock In · Time verified by an external provider");
  await employee.clockOut();await chapter("Clock Out · Attendance saved successfully");
  const day=latestPastWorkday();await employee.selectDate(day.date);
  await chapter("Select a past workday to request a correction");
  await employee.correctMissingDay(day.date);await chapter("09:00–17:00 · Sent for manager approval");
  await page.goto("/reports/monthly");await expect(page.getByRole("button",{name:"Download CSV"})).toBeVisible();
  await chapter("Monthly report · Reported and pending hours together");
  await page.screenshot({path:"test-results/demo-media/poster.png"});
  const download=page.waitForEvent("download");await page.getByRole("button",{name:"Download CSV"}).click();await download;
  await chapter("Export the payroll-period report as CSV");
  await recordingContext.clearCookies();await recordingContext.addCookies(state.sessions.TeamLead.cookies);
  const manager=new CorrectionReviewPage(page);await manager.open();
  await chapter("Amara Okafor · Manager reviews the requested hours");
  await manager.approve("Lucas Silva",day.date);await chapter("Correction approved · Reports update automatically");
  await recordingContext.clearCookies();await recordingContext.addCookies(state.sessions.Admin.cookies);
  await page.goto("/payroll/company-settings");await expect(page.getByLabel("Company time zone")).toBeVisible();
  await chapter("Alex Morgan · Configure time zone, payroll cycle and workweek");
  await page.goto("/admin/people");await expect(page.getByRole("heading",{name:"Add a company member"})).toBeVisible();
  await chapter("Invite your team · Assign roles and a direct manager");
  await recordingContext.close();recordingContext=undefined;
  await video.saveAs("test-results/demo-media/attendance-demo.webm");
  await writeFile("test-results/demo-media/README.txt","Recorded against a real local API and disposable SQL database. All people and company data are fictional. No Google credentials, real employee data or passwords are shown.\n");
 } finally {await recordingContext?.close();await workspace.dispose();}
});
