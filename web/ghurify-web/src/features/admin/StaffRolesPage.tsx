import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Crown, Lock, Pencil, Plus, ShieldCheck, Trash2, Users } from 'lucide-react';

import { asNumber } from '@/api/client';
import {
  cardClass,
  inputClass,
  primaryButtonClass,
  secondaryButtonClass,
} from '@/components/Field';
import { Dialog } from '@/components/Dialog';
import { EmptyState, ErrorState } from '@/components/States';
import { errorText } from '@/lib/errors';
import { formatCount, toLanguage } from '@/lib/format';
import {
  adminApi,
  type PermissionView,
  type SaveStaffRoleCommand,
  type StaffRoleView,
} from './adminApi';
import { permissionLabel, permissionGroupLabel } from './adminLabels';
import { useStepUp } from './useStepUp';
import { AdminPageHeader } from './AdminUi';

/**
 * The roles screen: what each job on the admin desk may do, and how to invent a new one.
 *
 * The permissions listed come from the API, not from this file, so a permission added in a
 * backend release shows up here without a frontend one. Permissions the signed-in person does
 * not hold themselves are shown but cannot be ticked: nobody hands out authority they lack, and
 * the API refuses it even if this screen were bypassed.
 */
export function StaffRolesPage() {
  const { t } = useTranslation();
  const [editing, setEditing] = useState<StaffRoleView | 'new' | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<StaffRoleView | null>(null);
  const stepUp = useStepUp();
  const queryClient = useQueryClient();

  const roles = useQuery({
    queryKey: ['admin', 'staff', 'roles'],
    queryFn: ({ signal }) => adminApi.staffRoles(signal),
  });

  const remove = useMutation({
    mutationFn: (id: number) => adminApi.deleteStaffRole(id),
    onSuccess: () => {
      setConfirmDelete(null);
      void queryClient.invalidateQueries({ queryKey: ['admin', 'staff'] });
    },
  });

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader
        eyebrow={t('admin.groups.system')}
        title={t('admin.roles.title')}
        description={t('admin.roles.lead')}
        actions={
          <button type="button" onClick={() => setEditing('new')} className={primaryButtonClass}>
            <Plus aria-hidden="true" className="h-4 w-4" />
            {t('admin.roles.create')}
          </button>
        }
      />

      {roles.isPending && (
        <div role="status" className="h-64 animate-pulse rounded-2xl bg-hill/10">
          <span className="sr-only">{t('common.loading')}</span>
        </div>
      )}

      {roles.isError && (
        <ErrorState message={errorText(roles.error, t)} onRetry={() => void roles.refetch()} />
      )}

      {roles.data?.roles.length === 0 && <EmptyState title={t('admin.roles.empty')} />}

      {roles.data && roles.data.roles.length > 0 && (
        <ul className="flex flex-col gap-3">
          {roles.data.roles.map((role) => (
            <RoleCard
              key={String(role.id)}
              role={role}
              onEdit={() => setEditing(role)}
              onDelete={() => setConfirmDelete(role)}
            />
          ))}
        </ul>
      )}

      {editing && roles.data && (
        <RoleEditor
          role={editing === 'new' ? null : editing}
          catalogue={roles.data.permissions}
          grantable={roles.data.grantablePermissions}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void queryClient.invalidateQueries({ queryKey: ['admin', 'staff'] });
          }}
        />
      )}

      {confirmDelete && (
        <Dialog open title={t('admin.roles.deleteTitle')} onClose={() => setConfirmDelete(null)}>
          <div className="flex flex-col gap-4">
            <p className="text-sm text-deep/80">
              {t('admin.roles.deleteBody', { name: confirmDelete.name })}
            </p>
            {remove.isError && (
              <p role="alert" className="text-sm font-medium text-jamdani">
                {errorText(remove.error, t)}
              </p>
            )}
            <div className="flex flex-wrap justify-end gap-2">
              <button
                type="button"
                onClick={() => setConfirmDelete(null)}
                className={secondaryButtonClass}
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                disabled={remove.isPending}
                onClick={() =>
                  void stepUp.run(() => remove.mutateAsync(asNumber(confirmDelete.id)))
                }
                className="rounded-full bg-jamdani px-6 py-2.5 font-semibold text-white shadow-sm transition hover:bg-jamdani/85 disabled:opacity-60"
              >
                {t('admin.roles.deleteConfirm')}
              </button>
            </div>
          </div>
        </Dialog>
      )}

      {stepUp.dialog}
    </div>
  );
}

/** One role: what it is, how many people hold it, and what it may do. */
function RoleCard({
  role,
  onEdit,
  onDelete,
}: {
  role: StaffRoleView;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const { t, i18n } = useTranslation();
  const language = toLanguage(i18n.language);
  const name = language === 'bn' ? role.nameBn : role.name;
  const description = language === 'bn' ? role.descriptionBn : role.description;

  return (
    <li className={`${cardClass} flex flex-col gap-3`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="flex flex-wrap items-center gap-2 font-display text-lg font-semibold text-deep">
            {role.isSuperAdmin && <Crown aria-hidden="true" className="h-4 w-4 text-turmeric" />}
            {name}
            {role.isSystem && (
              <span className="inline-flex items-center gap-1 rounded-full bg-mist px-2 py-0.5 text-[0.7rem] font-semibold uppercase tracking-wide text-deep/60 [:lang(bn)_&]:tracking-normal">
                <Lock aria-hidden="true" className="h-3 w-3" />
                {t('admin.roles.builtIn')}
              </span>
            )}
          </h3>
          {description && <p className="mt-1 text-sm text-deep/70">{description}</p>}
          <p className="mt-1 font-mono text-xs text-deep/50">{role.key}</p>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-mist px-3 py-1 text-sm font-semibold text-deep">
            <Users aria-hidden="true" className="h-4 w-4 text-hill" />
            {formatCount(Number(role.memberCount), language)}
            <span className="sr-only"> {t('admin.roles.members')}</span>
          </span>

          {!role.isSuperAdmin && (
            <button
              type="button"
              onClick={onEdit}
              className="rounded-full p-2 text-deep/70 transition hover:bg-mist hover:text-deep"
              aria-label={t('admin.roles.edit', { name })}
            >
              <Pencil aria-hidden="true" className="h-4 w-4" />
            </button>
          )}

          {!role.isSystem && (
            <button
              type="button"
              onClick={onDelete}
              className="rounded-full p-2 text-jamdani/80 transition hover:bg-jamdani/10 hover:text-jamdani"
              aria-label={t('admin.roles.delete', { name })}
            >
              <Trash2 aria-hidden="true" className="h-4 w-4" />
            </button>
          )}
        </div>
      </div>

      {role.isSuperAdmin ? (
        <p className="flex items-center gap-2 rounded-xl bg-turmeric/10 px-3 py-2 text-sm font-medium text-ochre">
          <ShieldCheck aria-hidden="true" className="h-4 w-4 shrink-0" />
          {t('admin.roles.superAdminHoldsEverything')}
        </p>
      ) : role.permissions.length === 0 ? (
        <p className="text-sm text-deep/60">{t('admin.roles.noPermissions')}</p>
      ) : (
        <ul className="flex flex-wrap gap-1.5">
          {role.permissions.map((permission) => (
            <li
              key={permission}
              className="rounded-full bg-hill/8 px-2.5 py-1 text-xs font-medium text-deep"
            >
              {permissionLabel(permission, t)}
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}

/** Create or change a role: its names, and the ticked list of what it may do. */
function RoleEditor({
  role,
  catalogue,
  grantable,
  onClose,
  onSaved,
}: {
  role: StaffRoleView | null;
  catalogue: readonly PermissionView[];
  grantable: readonly string[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const stepUp = useStepUp();

  const [key, setKey] = useState(role?.key ?? '');
  const [name, setName] = useState(role?.name ?? '');
  const [nameBn, setNameBn] = useState(role?.nameBn ?? '');
  const [description, setDescription] = useState(role?.description ?? '');
  const [descriptionBn, setDescriptionBn] = useState(role?.descriptionBn ?? '');
  const [chosen, setChosen] = useState<readonly string[]>(role?.permissions ?? []);

  const save = useMutation({
    mutationFn: (command: SaveStaffRoleCommand) =>
      role
        ? adminApi.updateStaffRole(asNumber(role.id), command)
        : adminApi.createStaffRole(command),
    onSuccess: onSaved,
  });

  const groups = [...new Set(catalogue.map((permission) => permission.group))];

  function toggle(permission: string) {
    setChosen((current) =>
      current.includes(permission)
        ? current.filter((held) => held !== permission)
        : [...current, permission],
    );
  }

  return (
    <Dialog
      open
      title={role ? t('admin.roles.editTitle') : t('admin.roles.createTitle')}
      onClose={onClose}
    >
      <form
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          void stepUp.run(() =>
            save.mutateAsync({
              key,
              name,
              nameBn,
              description: description || null,
              descriptionBn: descriptionBn || null,
              permissions: [...chosen],
            }),
          );
        }}
      >
        <div className="grid gap-3 sm:grid-cols-2">
          <Text
            id="role-name"
            label={t('admin.roles.name')}
            value={name}
            onChange={setName}
            maxLength={60}
            required
          />
          <Text
            id="role-name-bn"
            label={t('admin.roles.nameBn')}
            value={nameBn}
            onChange={setNameBn}
            maxLength={60}
            required
            lang="bn"
          />
        </div>

        <Text
          id="role-key"
          label={t('admin.roles.key')}
          hint={t('admin.roles.keyHint')}
          value={key}
          onChange={(next) => setKey(next.toLowerCase())}
          maxLength={40}
          required
          // A built-in role's key is matched by the seed script, so it cannot change.
          disabled={role?.isSystem === true}
        />

        <div className="grid gap-3 sm:grid-cols-2">
          <Text
            id="role-description"
            label={t('admin.roles.description')}
            value={description}
            onChange={setDescription}
            maxLength={300}
          />
          <Text
            id="role-description-bn"
            label={t('admin.roles.descriptionBn')}
            value={descriptionBn}
            onChange={setDescriptionBn}
            maxLength={300}
            lang="bn"
          />
        </div>

        <fieldset className="flex flex-col gap-3">
          <legend className="text-sm font-semibold text-deep">
            {t('admin.roles.permissions')}
          </legend>

          <div className="max-h-72 overflow-y-auto rounded-xl border border-hill/15 p-3">
            {groups.map((group) => (
              <div key={group} className="mb-4 last:mb-0">
                <p className="mb-1.5 text-[0.7rem] font-semibold uppercase tracking-[0.14em] text-deep/50 [:lang(bn)_&]:tracking-normal">
                  {permissionGroupLabel(group, t)}
                </p>
                <ul className="flex flex-col gap-1">
                  {catalogue
                    .filter((permission) => permission.group === group)
                    .map((permission) => {
                      const allowed = grantable.includes(permission.key);

                      return (
                        <li key={permission.key}>
                          <label
                            className={`flex items-start gap-2 rounded-lg px-2 py-1.5 text-sm ${
                              allowed ? 'text-deep hover:bg-mist' : 'text-deep/40'
                            }`}
                          >
                            <input
                              type="checkbox"
                              checked={chosen.includes(permission.key)}
                              disabled={!allowed}
                              onChange={() => toggle(permission.key)}
                              className="mt-0.5 h-4 w-4 shrink-0 accent-hill"
                            />
                            <span className="min-w-0">
                              {permissionLabel(permission.key, t)}
                              {permission.requiresStepUp && (
                                <span className="ml-1.5 text-xs text-ochre">
                                  {t('admin.roles.needsPassword')}
                                </span>
                              )}
                              {!allowed && (
                                <span className="ml-1.5 text-xs">{t('admin.roles.notYours')}</span>
                              )}
                            </span>
                          </label>
                        </li>
                      );
                    })}
                </ul>
              </div>
            ))}
          </div>
        </fieldset>

        {save.isError && (
          <p role="alert" className="text-sm font-medium text-jamdani">
            {errorText(save.error, t)}
          </p>
        )}

        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" onClick={onClose} className={secondaryButtonClass}>
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={save.isPending || stepUp.asking}
            className={`${primaryButtonClass} disabled:opacity-60`}
          >
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>

      {stepUp.dialog}
    </Dialog>
  );
}

/** A labelled text input. Local to this screen: the shared TextField is register-driven. */
function Text({
  id,
  label,
  hint,
  value,
  onChange,
  ...input
}: {
  id: string;
  label: string;
  hint?: string;
  value: string;
  onChange: (value: string) => void;
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'id'>) {
  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold text-deep">
        {label}
      </label>
      <input
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className={`${inputClass} disabled:bg-mist disabled:text-deep/50`}
        {...input}
      />
      {hint && <p className="text-xs text-deep/60">{hint}</p>}
    </div>
  );
}
