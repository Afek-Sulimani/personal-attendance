export function matchesEmployeeSearch(name: string, employeeNumber: string | null, search: string): boolean {
  const query = search.trim();
  if (!query) return true;
  if (name.toLocaleLowerCase().includes(query.toLocaleLowerCase())) return true;
  if (!employeeNumber) return false;
  if (employeeNumber.includes(query)) return true;
  return /^\d{1,5}$/.test(query) && Number(employeeNumber) === Number(query);
}
